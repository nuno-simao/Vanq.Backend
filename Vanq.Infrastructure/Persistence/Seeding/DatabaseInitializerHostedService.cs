using System;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Vanq.Infrastructure.Logging.Extensions;
using Vanq.Infrastructure.Persistence;

namespace Vanq.Infrastructure.Persistence.Seeding;

internal sealed class DatabaseInitializerHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializerHostedService> _logger;

    public DatabaseInitializerHostedService(IServiceProvider serviceProvider, ILogger<DatabaseInitializerHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Database initialization starting");

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            var migrationStopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Applying database migrations");
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            migrationStopwatch.Stop();
            _logger.LogPerformanceEvent("DatabaseMigrations", migrationStopwatch.ElapsedMilliseconds, threshold: 5000);
            _logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply database migrations");
            throw;
        }

        try
        {
            var seedingStopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Starting RBAC seed data initialization");
            var rbacSeeder = scope.ServiceProvider.GetRequiredService<RbacSeeder>();
            await rbacSeeder.SeedAsync(cancellationToken).ConfigureAwait(false);
            seedingStopwatch.Stop();
            _logger.LogPerformanceEvent("RbacSeeding", seedingStopwatch.ElapsedMilliseconds, threshold: 2000);
            _logger.LogInformation("RBAC seed data initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed RBAC data");
            throw;
        }

        try
        {
            var featureFlagStopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Starting feature flags seed data initialization");
            var featureFlagSeeder = scope.ServiceProvider.GetRequiredService<FeatureFlagsSeeder>();
            await featureFlagSeeder.SeedAsync(cancellationToken).ConfigureAwait(false);
            featureFlagStopwatch.Stop();
            _logger.LogPerformanceEvent("FeatureFlagsSeeding", featureFlagStopwatch.ElapsedMilliseconds, threshold: 1000);
            _logger.LogInformation("Feature flags seed data initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed feature flags data");
            throw;
        }

        totalStopwatch.Stop();
        _logger.LogPerformanceEvent("TotalDatabaseInitialization", totalStopwatch.ElapsedMilliseconds, threshold: 10000);
        _logger.LogInformation("Database initialization completed");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
