using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Persistence;

namespace Vanq.API.Tests;

public class VanqApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            // Add in-memory database for testing
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDatabase_" + Guid.NewGuid());
            });

            // Ensure database is created
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }

    public async Task EnableFeatureFlagAsync(string flagKey)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var flag = await context.FeatureFlags
            .FirstOrDefaultAsync(f => f.Key == flagKey && f.Environment == "Testing");

        if (flag == null)
        {
            flag = FeatureFlag.Create(
                key: flagKey,
                environment: "Testing",
                isEnabled: true,
                description: "Test flag",
                lastUpdatedBy: "Test",
                lastUpdatedAt: DateTime.UtcNow
            );
            await context.FeatureFlags.AddAsync(flag);
        }
        else
        {
            flag.Update(
                isEnabled: true,
                lastUpdatedBy: "Test",
                lastUpdatedAt: DateTime.UtcNow
            );
        }

        await context.SaveChangesAsync();
    }

    public async Task DisableFeatureFlagAsync(string flagKey)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var flag = await context.FeatureFlags
            .FirstOrDefaultAsync(f => f.Key == flagKey && f.Environment == "Testing");

        if (flag == null)
        {
            flag = FeatureFlag.Create(
                key: flagKey,
                environment: "Testing",
                isEnabled: false,
                description: "Test flag",
                lastUpdatedBy: "Test",
                lastUpdatedAt: DateTime.UtcNow
            );
            await context.FeatureFlags.AddAsync(flag);
        }
        else
        {
            flag.Update(
                isEnabled: false,
                lastUpdatedBy: "Test",
                lastUpdatedAt: DateTime.UtcNow
            );
        }

        await context.SaveChangesAsync();
    }
}
