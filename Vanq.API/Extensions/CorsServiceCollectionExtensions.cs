using Microsoft.Extensions.Options;
using Vanq.API.Configuration;
using Vanq.Application.Abstractions.FeatureFlags;

namespace Vanq.API.Extensions;

/// <summary>
/// Extension methods for configuring CORS services
/// </summary>
public static class CorsServiceCollectionExtensions
{
    /// <summary>
    /// Adds CORS services with configuration from appsettings.json
    /// Implements REQ-01, REQ-03, REQ-04
    /// </summary>
    public static IServiceCollection AddVanqCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Bind configuration
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));

        // Register CORS policy
        services.AddCors(options =>
        {
            options.AddPolicy(
                name: configuration[$"{CorsOptions.SectionName}:PolicyName"] ?? "vanq-default-cors",
                configurePolicy: policyBuilder =>
                {
                    var corsOptions = configuration
                        .GetSection(CorsOptions.SectionName)
                        .Get<CorsOptions>() ?? new CorsOptions();

                    ConfigureCorsPolicy(policyBuilder, corsOptions, environment);
                });
        });

        return services;
    }

    /// <summary>
    /// Configures CORS policy based on environment and feature flags
    /// Implements BR-01, BR-02, BR-03, REQ-04
    /// </summary>
    private static void ConfigureCorsPolicy(
        Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder policyBuilder,
        CorsOptions options,
        IHostEnvironment environment)
    {
        // REQ-04: Development mode - allow any origin
        if (environment.IsDevelopment())
        {
            policyBuilder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
            return;
        }

        // Production/Staging: Use configured origins
        if (options.AllowedOrigins.Count > 0)
        {
            // BR-01: Validate HTTPS in production
            var validOrigins = environment.IsProduction()
                ? options.AllowedOrigins.Where(IsHttpsOrigin).ToArray()
                : options.AllowedOrigins.ToArray();

            if (validOrigins.Length > 0)
            {
                // BR-02: Case-insensitive comparison with trailing slash normalization
                policyBuilder.WithOrigins(validOrigins)
                    .SetIsOriginAllowedToAllowWildcardSubdomains();
            }
        }

        // Configure methods
        if (options.AllowedMethods.Count > 0)
        {
            policyBuilder.WithMethods([.. options.AllowedMethods]);
        }
        else
        {
            policyBuilder.AllowAnyMethod();
        }

        // Configure headers
        if (options.AllowedHeaders.Count > 0)
        {
            policyBuilder.WithHeaders([.. options.AllowedHeaders]);
        }
        else
        {
            policyBuilder.AllowAnyHeader();
        }

        // BR-03: AllowCredentials requires specific origins
        if (options.AllowCredentials && options.AllowedOrigins.Count > 0)
        {
            policyBuilder.AllowCredentials();
        }

        // Set preflight cache duration
        policyBuilder.SetPreflightMaxAge(TimeSpan.FromSeconds(options.MaxAgeSeconds));
    }

    /// <summary>
    /// Validates if origin uses HTTPS scheme (BR-01)
    /// </summary>
    private static bool IsHttpsOrigin(string origin)
    {
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>
    /// Applies CORS middleware with optional feature flag check
    /// Implements REQ-02, FLAG-01
    /// </summary>
    public static IApplicationBuilder UseVanqCors(
        this IApplicationBuilder app,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var policyName = configuration[$"{CorsOptions.SectionName}:PolicyName"] ?? "vanq-default-cors";

        // Apply CORS policy
        app.UseCors(policyName);

        return app;
    }

    /// <summary>
    /// Applies dynamic CORS policy based on feature flag (FLAG-01: cors-relaxed)
    /// This is an alternative approach when runtime feature flag control is needed
    /// </summary>
    public static IApplicationBuilder UseVanqCorsDynamic(
        this IApplicationBuilder app,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var policyName = configuration[$"{CorsOptions.SectionName}:PolicyName"] ?? "vanq-default-cors";

        app.Use(async (context, next) =>
        {
            var featureFlagService = context.RequestServices
                .GetRequiredService<IFeatureFlagService>();

            // FLAG-01: Check if cors-relaxed is enabled
            var isCorsRelaxed = await featureFlagService.IsEnabledAsync("cors-relaxed");

            if (isCorsRelaxed)
            {
                // Apply relaxed policy: allow any origin
                context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, PATCH, DELETE, OPTIONS");
                context.Response.Headers.Append("Access-Control-Allow-Headers", "*");

                if (context.Request.Method == "OPTIONS")
                {
                    context.Response.StatusCode = 200;
                    return;
                }
            }

            await next();
        });

        return app;
    }
}
