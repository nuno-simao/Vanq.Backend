using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
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
        _logger.LogInformation("Database initialization starting");

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            _logger.LogInformation("Applying database migrations");
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply database migrations");
            throw;
        }

        try
        {
            _logger.LogInformation("Starting RBAC seed data initialization");
            var seeder = scope.ServiceProvider.GetRequiredService<RbacSeeder>();
            await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("RBAC seed data initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed RBAC data");
            throw;
        }

        _logger.LogInformation("Database initialization completed");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
