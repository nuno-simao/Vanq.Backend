using System.Reflection;
using Shouldly;
using Microsoft.Extensions.Logging;
using Vanq.Application.Abstractions.FeatureFlags;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Rbac;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Rbac;
using Xunit;

namespace Vanq.Infrastructure.Tests.Authorization;

public class PermissionCheckerTests
{
    [Fact]
    public async Task EnsurePermissionAsync_ShouldLogWarning_WhenPermissionMissing()
    {
        // Arrange
        var permission = "rbac:role:update";
        var user = User.Create("user@example.com", "hash", DateTime.UtcNow);
        var userId = user.Id;
        var repository = new StubUserRepository(user);
        var featureFlagService = new StubFeatureFlagService(isEnabled: true);
        var logger = new TestLogger<PermissionChecker>();
        var checker = new PermissionChecker(repository, featureFlagService, logger);

        // Act
        Func<Task> act = () => checker.EnsurePermissionAsync(userId, permission, CancellationToken.None);

        // Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(act);

        logger.Entries.Count.ShouldBe(1);
        var entry = logger.Entries.Single();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain(userId.ToString(), Case.Insensitive);
        entry.Message.ShouldContain(permission, Case.Insensitive);
    }

    [Fact]
    public async Task EnsurePermissionAsync_ShouldNotLog_WhenPermissionExists()
    {
        var permission = "rbac:role:read";
        var timestamp = DateTimeOffset.UtcNow;
        var role = Role.Create("admin", "Admin", null, false, timestamp);
        var permissionEntity = Permission.Create(permission, "Role Read", null, timestamp);
        role.AddPermission(permissionEntity.Id, Guid.NewGuid(), timestamp);
        // attach permission entity to role permission for matching
        var rolePermission = role.Permissions.First();
        typeof(RolePermission).GetProperty(nameof(RolePermission.Permission), BindingFlags.Instance | BindingFlags.Public)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(rolePermission, new object?[] { permissionEntity });

        var user = User.Create("user@example.com", "hash", DateTime.UtcNow);
        var userId = user.Id;
        user.AssignRole(role.Id, Guid.NewGuid(), timestamp);
        // attach role entity to user role assignment
        var userRole = user.Roles.First();
        typeof(UserRole).GetProperty(nameof(UserRole.Role), BindingFlags.Instance | BindingFlags.Public)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(userRole, new object?[] { role });

        var repository = new StubUserRepository(user);
        var featureFlagService = new StubFeatureFlagService(isEnabled: true);
        var logger = new TestLogger<PermissionChecker>();
        var checker = new PermissionChecker(repository, featureFlagService, logger);

        await checker.EnsurePermissionAsync(userId, permission, CancellationToken.None);

        logger.Entries.ShouldBeEmpty();
    }

    private sealed class StubUserRepository : IUserRepository
    {
        private readonly User _user;

        public StubUserRepository(User user)
        {
            _user = user;
        }

        public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult<User?>(id == _user.Id ? _user : null);
        }

        public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<User?> GetByEmailWithRolesAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<bool> ExistsByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
        public void Update(User user) { }
    }

    private sealed class StubFeatureFlagService : IFeatureFlagService
    {
        private readonly bool _isEnabled;

        public StubFeatureFlagService(bool isEnabled)
        {
            _isEnabled = isEnabled;
        }

        public Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_isEnabled);

        public Task<bool> GetFlagOrDefaultAsync(string key, bool defaultValue = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(defaultValue);

        public Task<Application.Contracts.FeatureFlags.FeatureFlagDto?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<Application.Contracts.FeatureFlags.FeatureFlagDto?>(null);

        public Task<List<Application.Contracts.FeatureFlags.FeatureFlagDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Application.Contracts.FeatureFlags.FeatureFlagDto>());

        public Task<List<Application.Contracts.FeatureFlags.FeatureFlagDto>> GetByEnvironmentAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Application.Contracts.FeatureFlags.FeatureFlagDto>());

        public Task<Application.Contracts.FeatureFlags.FeatureFlagDto> CreateAsync(Application.Contracts.FeatureFlags.CreateFeatureFlagDto request, string? updatedBy = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Application.Contracts.FeatureFlags.FeatureFlagDto?> UpdateAsync(string key, Application.Contracts.FeatureFlags.UpdateFeatureFlagDto request, string? updatedBy = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Application.Contracts.FeatureFlags.FeatureFlagDto?> ToggleAsync(string key, string? updatedBy = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }

        public List<LogEntry> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private readonly record struct LogEntry(LogLevel Level, string Message);
}

