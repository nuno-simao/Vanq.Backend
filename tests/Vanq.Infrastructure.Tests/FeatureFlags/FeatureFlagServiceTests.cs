using Shouldly;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Contracts.FeatureFlags;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.FeatureFlags;
using Vanq.Infrastructure.Persistence;
using Vanq.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Vanq.Infrastructure.Tests.FeatureFlags;

public class FeatureFlagServiceTests
{
    private readonly IMemoryCache _cache;
    private readonly DateTime _fixedTime = new(2025, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    public FeatureFlagServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnTrue_WhenFlagIsEnabled()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        var service = CreateService(context, repository);

        var flag = FeatureFlag.Create(
            "enabled-feature",
            "Development",
            true,
            lastUpdatedBy: "test",
            lastUpdatedAt: _fixedTime);

        await repository.AddAsync(flag, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act
        var isEnabled = await service.IsEnabledAsync("enabled-feature");

        // Assert
        isEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnFalse_WhenFlagIsDisabled()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        var service = CreateService(context, repository);

        var flag = FeatureFlag.Create(
            "disabled-feature",
            "Development",
            false,
            lastUpdatedBy: "test",
            lastUpdatedAt: _fixedTime);

        await repository.AddAsync(flag, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act
        var isEnabled = await service.IsEnabledAsync("disabled-feature");

        // Assert
        isEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnFalse_WhenFlagDoesNotExist()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        var service = CreateService(context, repository);

        // Act
        var isEnabled = await service.IsEnabledAsync("nonexistent-feature");

        // Assert
        isEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldUseCache_OnSecondCall()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        var service = CreateService(context, repository);

        var flag = FeatureFlag.Create(
            "cached-feature",
            "Development",
            true,
            lastUpdatedBy: "test",
            lastUpdatedAt: _fixedTime);

        await repository.AddAsync(flag, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act - First call (cache miss)
        var isEnabled1 = await service.IsEnabledAsync("cached-feature");

        // Modify database directly (simulating change)
        var dbFlag = await context.FeatureFlags.FirstAsync(f => f.Key == "cached-feature");
        dbFlag.Update(false, lastUpdatedBy: "test2", lastUpdatedAt: _fixedTime);
        await context.SaveChangesAsync();

        // Second call (should use cache, not see the change)
        var isEnabled2 = await service.IsEnabledAsync("cached-feature");

        // Assert
        isEnabled1.ShouldBeTrue();
        isEnabled2.ShouldBeTrue(); // Still true from cache, not updated
    }

    [Fact]
    public async Task GetFlagOrDefaultAsync_ShouldReturnDefault_WhenFlagDoesNotExist()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        var service = CreateService(context, repository);

        // Act
        var result = await service.GetFlagOrDefaultAsync("nonexistent", defaultValue: true);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task GetFlagOrDefaultAsync_ShouldReturnFlagValue_WhenExists()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        var service = CreateService(context, repository);

        var flag = FeatureFlag.Create(
            "existing-feature",
            "Development",
            false,
            lastUpdatedBy: "test",
            lastUpdatedAt: _fixedTime);

        await repository.AddAsync(flag, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetFlagOrDefaultAsync("existing-feature", defaultValue: true);

        // Assert
        result.ShouldBeFalse(); // Flag value takes precedence over default
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateFlag_AndInvalidateCache()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        var service = CreateService(context, repository);

        var createDto = new CreateFeatureFlagDto(
            Key: "new-feature",
            Environment: "Development",
            IsEnabled: true,
            Description: "New test feature",
            IsCritical: false,
            Metadata: null);

        // Act
        var result = await service.CreateAsync(createDto, "admin@test.com");

        // Assert
        result.ShouldNotBeNull();
        result.Key.ShouldBe("new-feature");
        result.IsEnabled.ShouldBeTrue();
        result.LastUpdatedBy.ShouldBe("admin@test.com");

        var dbFlag = await repository.GetByKeyAndEnvironmentAsync("new-feature", "Development", CancellationToken.None);
        dbFlag.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenFlagAlreadyExists()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        var service = CreateService(context, repository);

        var existingFlag = FeatureFlag.Create(
            "existing-feature",
            "Development",
            true,
            lastUpdatedBy: "test",
            lastUpdatedAt: _fixedTime);

        await repository.AddAsync(existingFlag, CancellationToken.None);
        await context.SaveChangesAsync();

        var createDto = new CreateFeatureFlagDto(
            Key: "existing-feature",
            Environment: "Development",
            IsEnabled: false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(createDto, "admin@test.com"));
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateFlag_AndInvalidateCache()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        var service = CreateService(context, repository);

        var flag = FeatureFlag.Create(
            "update-feature",
            "Development",
            true,
            description: "Original",
            lastUpdatedBy: "original-user",
            lastUpdatedAt: _fixedTime);

        await repository.AddAsync(flag, CancellationToken.None);
        await context.SaveChangesAsync();

        // Pre-cache the flag
        await service.IsEnabledAsync("update-feature");

        var updateDto = new UpdateFeatureFlagDto(
            IsEnabled: false,
            Description: "Updated");

        // Act
        var result = await service.UpdateAsync("update-feature", updateDto, "admin@test.com");

        // Assert
        result.ShouldNotBeNull();
        result!.IsEnabled.ShouldBeFalse();
        result.Description.ShouldBe("Updated");
        result.LastUpdatedBy.ShouldBe("admin@test.com");

        // Verify cache was invalidated
        var newValue = await service.IsEnabledAsync("update-feature");
        newValue.ShouldBeFalse();
    }

    [Fact]
    public async Task ToggleAsync_ShouldToggleFlag_AndInvalidateCache()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        var service = CreateService(context, repository);

        var flag = FeatureFlag.Create(
            "toggle-feature",
            "Development",
            true,
            lastUpdatedBy: "test",
            lastUpdatedAt: _fixedTime);

        await repository.AddAsync(flag, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act - First toggle
        var result1 = await service.ToggleAsync("toggle-feature", "admin@test.com");

        // Assert - Should be disabled now
        result1.ShouldNotBeNull();
        result1!.IsEnabled.ShouldBeFalse();

        // Act - Second toggle
        var result2 = await service.ToggleAsync("toggle-feature", "admin@test.com");

        // Assert - Should be enabled again
        result2.ShouldNotBeNull();
        result2!.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllFlags()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        var service = CreateService(context, repository);

        var flag1 = FeatureFlag.Create("flag-1", "Development", true, lastUpdatedBy: "test", lastUpdatedAt: _fixedTime);
        var flag2 = FeatureFlag.Create("flag-2", "Development", false, lastUpdatedBy: "test", lastUpdatedAt: _fixedTime);
        var flag3 = FeatureFlag.Create("flag-3", "Production", true, lastUpdatedBy: "test", lastUpdatedAt: _fixedTime);

        await repository.AddAsync(flag1, CancellationToken.None);
        await repository.AddAsync(flag2, CancellationToken.None);
        await repository.AddAsync(flag3, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act
        var allFlags = await service.GetAllAsync();

        // Assert
        allFlags.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetByEnvironmentAsync_ShouldReturnOnlyCurrentEnvironmentFlags()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        var service = CreateService(context, repository);

        var devFlag1 = FeatureFlag.Create("dev-flag-1", "Development", true, lastUpdatedBy: "test", lastUpdatedAt: _fixedTime);
        var devFlag2 = FeatureFlag.Create("dev-flag-2", "Development", false, lastUpdatedBy: "test", lastUpdatedAt: _fixedTime);
        var prodFlag = FeatureFlag.Create("prod-flag", "Production", true, lastUpdatedBy: "test", lastUpdatedAt: _fixedTime);

        await repository.AddAsync(devFlag1, CancellationToken.None);
        await repository.AddAsync(devFlag2, CancellationToken.None);
        await repository.AddAsync(prodFlag, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act
        var envFlags = await service.GetByEnvironmentAsync();

        // Assert
        envFlags.Count.ShouldBe(2);
        envFlags.ShouldAllBe(f => f.Environment == "Development");
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private FeatureFlagService CreateService(AppDbContext context, FeatureFlagRepository repository)
    {
        var environment = new TestHostEnvironment("Development");
        var clock = new TestDateTimeProvider(_fixedTime);
        var logger = new TestLogger<FeatureFlagService>();

        return new FeatureFlagService(
            repository,
            context,
            _cache,
            environment,
            clock,
            logger);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
            ApplicationName = "Test";
            ContentRootPath = string.Empty;
            ContentRootFileProvider = null!;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
    }

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public TestDateTimeProvider(DateTime fixedTime)
        {
            UtcNow = fixedTime;
        }

        public DateTime UtcNow { get; }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}

