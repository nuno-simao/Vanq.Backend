using Shouldly;
using Microsoft.EntityFrameworkCore;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Persistence;
using Vanq.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Vanq.Infrastructure.Tests.Persistence;

public class FeatureFlagRepositoryTests
{
    [Fact]
    public async Task GetByKeyAndEnvironmentAsync_ShouldReturnFlag_WhenExists()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        
        var flag = FeatureFlag.Create(
            key: "test-feature",
            environment: "Development",
            isEnabled: true,
            description: "Test flag",
            isCritical: false,
            lastUpdatedBy: "test-user",
            lastUpdatedAt: DateTime.UtcNow);

        await repository.AddAsync(flag, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act
        var fetched = await repository.GetByKeyAndEnvironmentAsync(
            "test-feature",
            "Development",
            CancellationToken.None);

        // Assert
        fetched.ShouldNotBeNull();
        fetched!.Key.ShouldBe("test-feature");
        fetched.Environment.ShouldBe("Development");
        fetched.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetByKeyAndEnvironmentAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);

        // Act
        var fetched = await repository.GetByKeyAndEnvironmentAsync(
            "nonexistent-feature",
            "Development",
            CancellationToken.None);

        // Assert
        fetched.ShouldBeNull();
    }

    [Fact]
    public async Task GetByKeyAndEnvironmentAsync_ShouldBeEnvironmentSpecific()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        
        var devFlag = FeatureFlag.Create(
            key: "test-feature",
            environment: "Development",
            isEnabled: true,
            lastUpdatedBy: "test-user",
            lastUpdatedAt: DateTime.UtcNow);

        var prodFlag = FeatureFlag.Create(
            key: "test-feature",
            environment: "Production",
            isEnabled: false,
            lastUpdatedBy: "test-user",
            lastUpdatedAt: DateTime.UtcNow);

        await repository.AddAsync(devFlag, CancellationToken.None);
        await repository.AddAsync(prodFlag, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act
        var devFetched = await repository.GetByKeyAndEnvironmentAsync(
            "test-feature",
            "Development",
            CancellationToken.None);

        var prodFetched = await repository.GetByKeyAndEnvironmentAsync(
            "test-feature",
            "Production",
            CancellationToken.None);

        // Assert
        devFetched.ShouldNotBeNull();
        devFetched!.IsEnabled.ShouldBeTrue();

        prodFetched.ShouldNotBeNull();
        prodFetched!.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByKeyAndEnvironmentAsync_ShouldReturnTrue_WhenExists()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        
        var flag = FeatureFlag.Create(
            key: "existing-feature",
            environment: "Development",
            isEnabled: true,
            lastUpdatedBy: "test-user",
            lastUpdatedAt: DateTime.UtcNow);

        await repository.AddAsync(flag, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act
        var exists = await repository.ExistsByKeyAndEnvironmentAsync(
            "existing-feature",
            "Development",
            CancellationToken.None);

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByKeyAndEnvironmentAsync_ShouldReturnFalse_WhenNotExists()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);

        // Act
        var exists = await repository.ExistsByKeyAndEnvironmentAsync(
            "nonexistent-feature",
            "Development",
            CancellationToken.None);

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task GetByEnvironmentAsync_ShouldReturnOnlyFlagsForSpecifiedEnvironment()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        
        var devFlag1 = FeatureFlag.Create("dev-feature-1", "Development", true, lastUpdatedBy: "test", lastUpdatedAt: DateTime.UtcNow);
        var devFlag2 = FeatureFlag.Create("dev-feature-2", "Development", false, lastUpdatedBy: "test", lastUpdatedAt: DateTime.UtcNow);
        var prodFlag = FeatureFlag.Create("prod-feature", "Production", true, lastUpdatedBy: "test", lastUpdatedAt: DateTime.UtcNow);

        await repository.AddAsync(devFlag1, CancellationToken.None);
        await repository.AddAsync(devFlag2, CancellationToken.None);
        await repository.AddAsync(prodFlag, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act
        var devFlags = await repository.GetByEnvironmentAsync("Development", CancellationToken.None);

        // Assert
        devFlags.Count.ShouldBe(2);
        devFlags.ShouldAllBe(f => f.Environment == "Development");
        devFlags.ShouldContain(f => f.Key == "dev-feature-1");
        devFlags.ShouldContain(f => f.Key == "dev-feature-2");
    }

    [Fact]
    public async Task Update_ShouldPersistChanges()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        
        var flag = FeatureFlag.Create(
            key: "update-test",
            environment: "Development",
            isEnabled: true,
            description: "Original description",
            lastUpdatedBy: "original-user",
            lastUpdatedAt: DateTime.UtcNow);

        await repository.AddAsync(flag, CancellationToken.None);
        await context.SaveChangesAsync();

        // Detach to simulate getting from a different context
        context.Entry(flag).State = EntityState.Detached;

        // Act
        var fetchedForUpdate = await context.FeatureFlags
            .FirstAsync(f => f.Key == "update-test" && f.Environment == "Development");

        fetchedForUpdate.Update(
            isEnabled: false,
            description: "Updated description",
            lastUpdatedBy: "new-user",
            lastUpdatedAt: DateTime.UtcNow);

        repository.Update(fetchedForUpdate);
        await context.SaveChangesAsync();

        // Assert
        var updated = await repository.GetByKeyAndEnvironmentAsync(
            "update-test",
            "Development",
            CancellationToken.None);

        updated.ShouldNotBeNull();
        updated!.IsEnabled.ShouldBeFalse();
        updated.Description.ShouldBe("Updated description");
        updated.LastUpdatedBy.ShouldBe("new-user");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllFlags()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        
        var flag1 = FeatureFlag.Create("feature-1", "Development", true, lastUpdatedBy: "test", lastUpdatedAt: DateTime.UtcNow);
        var flag2 = FeatureFlag.Create("feature-2", "Development", false, lastUpdatedBy: "test", lastUpdatedAt: DateTime.UtcNow);
        var flag3 = FeatureFlag.Create("feature-1", "Production", true, lastUpdatedBy: "test", lastUpdatedAt: DateTime.UtcNow);

        await repository.AddAsync(flag1, CancellationToken.None);
        await repository.AddAsync(flag2, CancellationToken.None);
        await repository.AddAsync(flag3, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act
        var allFlags = await repository.GetAllAsync(CancellationToken.None);

        // Assert
        allFlags.Count.ShouldBe(3);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}

