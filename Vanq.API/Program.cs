using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Vanq.API.Endpoints;
using Vanq.API.OpenApi;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.FeatureFlags;
using Vanq.Application.Abstractions.Rbac;
using Vanq.Infrastructure.DependencyInjection;
using Vanq.Infrastructure.Logging;
using Vanq.Infrastructure.Logging.Middleware;
using Vanq.Infrastructure.Rbac;
using Vanq.Shared.Security;

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithProcessId()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Vanq.API application");

    var builder = WebApplication.CreateBuilder(args);

    // Replace default logging with Serilog
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        var loggingOptions = context.Configuration
            .GetSection("StructuredLogging")
            .Get<LoggingOptions>() ?? new LoggingOptions();

        var minimumLevel = Enum.TryParse<LogEventLevel>(loggingOptions.MinimumLevel, out var level)
            ? level
            : LogEventLevel.Information;

        configuration
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", "Vanq.API")
            .Enrich.WithProperty("Version", "1.0.0");

        if (loggingOptions.ConsoleJson)
        {
            configuration.WriteTo.Console(
                new Serilog.Formatting.Compact.CompactJsonFormatter());
        }
        else
        {
            configuration.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }

        if (!string.IsNullOrWhiteSpace(loggingOptions.FilePath))
        {
            configuration.WriteTo.File(
                new Serilog.Formatting.Compact.CompactJsonFormatter(),
                loggingOptions.FilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 100_000_000,
                rollOnFileSizeLimit: true);
        }
    });

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(new BearerAuthenticationDocumentTransformer());
});

builder.Services.AddEndpointsApiExplorer();

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection.GetValue<string>("SigningKey")!));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection.GetValue<string>("Issuer"),
            ValidateAudience = true,
            ValidAudience = jwtSection.GetValue<string>("Audience"),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal is null || !principal.TryGetUserId(out var userId))
                {
                    context.Fail("Missing user context");
                    return;
                }

                var securityStampClaim = principal.FindFirst("security_stamp")?.Value;
                if (string.IsNullOrWhiteSpace(securityStampClaim))
                {
                    context.Fail("Missing security stamp");
                    return;
                }

                var serviceProvider = context.HttpContext.RequestServices;
                var userRepository = serviceProvider.GetRequiredService<IUserRepository>();
                var user = await userRepository.GetByIdWithRolesAsync(userId, context.HttpContext.RequestAborted).ConfigureAwait(false);

                if (user is null || !user.IsActive)
                {
                    context.Fail("User not found or inactive");
                    return;
                }

                if (!string.Equals(user.SecurityStamp, securityStampClaim, StringComparison.Ordinal))
                {
                    context.Fail("Security stamp mismatch");
                    return;
                }

                var featureFlagService = serviceProvider.GetRequiredService<IFeatureFlagService>();
                if (!await featureFlagService.IsEnabledAsync("rbac-enabled"))
                {
                    return;
                }

                var tokenRolesStamp = principal.FindFirst("roles_stamp")?.Value ?? string.Empty;
                var (roles, permissions, rolesStamp) = RbacTokenPayloadBuilder.Build(user);

                if (!string.Equals(rolesStamp, tokenRolesStamp, StringComparison.Ordinal))
                {
                    context.Fail("RBAC permissions outdated");
                    return;
                }

                if (principal.Identity is ClaimsIdentity identity)
                {
                    foreach (var claim in identity.FindAll(ClaimTypes.Role).ToList())
                    {
                        identity.RemoveClaim(claim);
                    }

                    foreach (var claim in identity.FindAll("permission").ToList())
                    {
                        identity.RemoveClaim(claim);
                    }

                    foreach (var role in roles)
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, role));
                    }

                    foreach (var permission in permissions)
                    {
                        identity.AddClaim(new Claim("permission", permission));
                    }
                }
            }
        };
    });

    builder.Services.AddAuthorization();

    // Register LoggingOptions for DI
    builder.Services.Configure<LoggingOptions>(
        builder.Configuration.GetSection("StructuredLogging"));

    // Register SensitiveDataRedactor
    builder.Services.AddSingleton<SensitiveDataRedactor>();

    var app = builder.Build();

    // Add request logging middleware
    app.UseMiddleware<RequestResponseLoggingMiddleware>();

    app.UseSerilogRequestLogging();

    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Vanq API Reference");
    });

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGet("/", () => Results.Redirect("/scalar"))
       .ExcludeFromDescription();

    app.MapAllEndpoints();

    Log.Information("Vanq.API started successfully");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
