using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vanq.Application.Abstractions.Auth;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Rbac;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Abstractions.Tokens;
using Vanq.Application.Configuration;
using Vanq.Infrastructure.Auth;
using Vanq.Infrastructure.Auth.Jwt;
using Vanq.Infrastructure.Auth.Password;
using Vanq.Infrastructure.Auth.Tokens;
using Vanq.Infrastructure.Persistence;
using Vanq.Infrastructure.Persistence.Seeding;
using Vanq.Infrastructure.Persistence.Repositories;
using Vanq.Infrastructure.Rbac;

namespace Vanq.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<RbacOptions>(configuration.GetSection(RbacOptions.SectionName));
        services.Configure<RbacSeedOptions>(configuration.GetSection(RbacSeedOptions.SectionName));

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<RbacSeeder>();
        services.AddScoped<IRbacFeatureManager, RbacFeatureManager>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IPermissionChecker, PermissionChecker>();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuthRefreshService, AuthRefreshService>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddHostedService<DatabaseInitializerHostedService>();

        return services;
    }

    private sealed class SystemDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
