using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Persistence;

namespace Vanq.Infrastructure.Persistence.Seeding;

internal sealed class FeatureFlagsSeeder
{
    private readonly AppDbContext _context;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<FeatureFlagsSeeder> _logger;

    public FeatureFlagsSeeder(
        AppDbContext context,
        IHostEnvironment environment,
        ILogger<FeatureFlagsSeeder> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var currentEnvironment = _environment.EnvironmentName;

        await SeedFlagIfNotExistsAsync(
            key: "rbac-enabled",
            environment: currentEnvironment,
            isEnabled: true,
            description: "Enables Role-Based Access Control (RBAC) permission checks",
            lastUpdatedAt: now,
            cancellationToken: cancellationToken
        );

        await SeedFlagIfNotExistsAsync(
            key: "problem-details-enabled",
            environment: currentEnvironment,
            isEnabled: false, // Start disabled for gradual rollout
            description: "Enables RFC 7807 Problem Details for error responses",
            lastUpdatedAt: now,
            cancellationToken: cancellationToken
        );

        await SeedFlagIfNotExistsAsync(
            key: "cors-relaxed",
            environment: currentEnvironment,
            isEnabled: false, // Disabled by default - use IsDevelopment() for relaxed mode
            description: "Enables relaxed CORS policy (allow any origin) - use only for dev/staging",
            lastUpdatedAt: now,
            cancellationToken: cancellationToken
        );

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Feature flags seeded successfully for environment {Environment}",
            currentEnvironment
        );
    }

    private async Task SeedFlagIfNotExistsAsync(
        string key,
        string environment,
        bool isEnabled,
        string description,
        DateTime lastUpdatedAt,
        CancellationToken cancellationToken)
    {
        var exists = await _context.FeatureFlags
            .AnyAsync(f => f.Key == key && f.Environment == environment, cancellationToken);

        if (!exists)
        {
            var flag = FeatureFlag.Create(
                key: key,
                environment: environment,
                isEnabled: isEnabled,
                description: description,
                lastUpdatedBy: "System",
                lastUpdatedAt: lastUpdatedAt
            );
            await _context.FeatureFlags.AddAsync(flag, cancellationToken);

            _logger.LogInformation(
                "Seeding feature flag: {Key} in {Environment} (Enabled: {IsEnabled})",
                key,
                environment,
                isEnabled
            );
        }
    }
}
