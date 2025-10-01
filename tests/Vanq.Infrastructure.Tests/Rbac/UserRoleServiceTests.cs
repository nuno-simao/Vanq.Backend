using Shouldly;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vanq.Application.Abstractions.FeatureFlags;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Rbac;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Configuration;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Rbac;
using Xunit;

namespace Vanq.Infrastructure.Tests.Rbac;

public class UserRoleServiceTests
{
    [Fact]
    public async Task AssignRoleAsync_ShouldAssignRole_WhenUserAndRoleExist()
    {
        // Arrange
        var user = User.Create("user@example.com", "hash", DateTime.UtcNow);
        var role = Role.Create("admin", "Admin", null, false, DateTimeOffset.UtcNow);
        var executorId = Guid.NewGuid();
        var userRepository = StubUserRepository.WithUser(user);
        var roleRepository = StubRoleRepository.WithRole(role);
        var unitOfWork = new StubUnitOfWork();
        var featureFlagService = new StubFeatureFlagService(isEnabled: true);
        var clock = new StubClock(DateTime.UtcNow);
        var options = Options.Create(new RbacOptions { DefaultRole = "viewer" });
        var service = CreateService(userRepository, roleRepository, unitOfWork, clock, featureFlagService, options);

        // Act
        await service.AssignRoleAsync(user.Id, role.Id, executorId, CancellationToken.None);

        // Assert
        featureFlagService.IsEnabledCallCount.ShouldBe(1);
        userRepository.UpdateCallCount.ShouldBe(1);
        unitOfWork.SaveChangesCallCount.ShouldBe(1);
        user.HasActiveRole(role.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task AssignRoleAsync_ShouldBeIdempotent_WhenRoleAlreadyAssigned()
    {
        var user = User.Create("user@example.com", "hash", DateTime.UtcNow);
        var role = Role.Create("admin", "Admin", null, false, DateTimeOffset.UtcNow);
        var executorId = Guid.NewGuid();
        user.AssignRole(role.Id, executorId, DateTimeOffset.UtcNow);

        var userRepository = StubUserRepository.WithUser(user);
        var roleRepository = StubRoleRepository.WithRole(role);
        var unitOfWork = new StubUnitOfWork();
        var featureFlagService = new StubFeatureFlagService(isEnabled: true);
        var clock = new StubClock(DateTime.UtcNow);
        var options = Options.Create(new RbacOptions { DefaultRole = "viewer" });
        var service = CreateService(userRepository, roleRepository, unitOfWork, clock, featureFlagService, options);

        await service.AssignRoleAsync(user.Id, role.Id, executorId, CancellationToken.None);

        userRepository.UpdateCallCount.ShouldBe(0);
        unitOfWork.SaveChangesCallCount.ShouldBe(0);
        user.Roles.Count(roleAssignment => roleAssignment.RoleId == role.Id && roleAssignment.IsActive)
            .ShouldBe(1);
    }

    [Fact]
    public async Task RevokeRoleAsync_ShouldAssignDefaultRole_WhenNoActiveRolesRemain()
    {
        var timestamp = DateTime.UtcNow;
        var user = User.Create("user@example.com", "hash", timestamp);
        var primaryRole = Role.Create("manager", "Manager", null, false, DateTimeOffset.UtcNow);
        var defaultRole = Role.Create("viewer", "Viewer", null, false, DateTimeOffset.UtcNow);
        var executorId = Guid.NewGuid();
        user.AssignRole(primaryRole.Id, executorId, DateTimeOffset.UtcNow);

        var userRepository = StubUserRepository.WithUser(user);
        var roleRepository = new StubRoleRepository
        {
            RoleById = primaryRole,
            RoleByName = defaultRole
        };
        var unitOfWork = new StubUnitOfWork();
        var featureFlagService = new StubFeatureFlagService(isEnabled: true);
        var clock = new StubClock(timestamp);
        var options = Options.Create(new RbacOptions { DefaultRole = defaultRole.Name });
        var service = CreateService(userRepository, roleRepository, unitOfWork, clock, featureFlagService, options);

        await service.RevokeRoleAsync(user.Id, primaryRole.Id, executorId, CancellationToken.None);

        user.HasActiveRole(primaryRole.Id).ShouldBeFalse();
        user.HasActiveRole(defaultRole.Id).ShouldBeTrue();
        userRepository.UpdateCallCount.ShouldBe(1);
        unitOfWork.SaveChangesCallCount.ShouldBe(1);
    }

    private static UserRoleService CreateService(
        StubUserRepository userRepository,
        StubRoleRepository roleRepository,
        StubUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IFeatureFlagService featureFlagService,
        IOptions<RbacOptions> options)
    {
        return new UserRoleService(
            userRepository,
            roleRepository,
            unitOfWork,
            clock,
            featureFlagService,
            options,
            NullLogger<UserRoleService>.Instance);
    }

    private sealed class StubUserRepository : IUserRepository
    {
        private StubUserRepository(User user)
        {
            User = user;
        }

        public User User { get; }
        public int UpdateCallCount { get; private set; }

        public static StubUserRepository WithUser(User user) => new(user);

        public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<User?> GetByEmailWithRolesAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<User?>(id == User.Id ? User : null);
        public Task<bool> ExistsByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
        public void Update(User user)
        {
            UpdateCallCount++;
        }
    }

    private sealed class StubRoleRepository : IRoleRepository
    {
        public Role? RoleById { get; set; }
        public Role? RoleByName { get; set; }

        public static StubRoleRepository WithRole(Role role) => new() { RoleById = role };

        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Role>>(Array.Empty<Role>());
        public Task<IReadOnlyList<Role>> GetAllWithPermissionsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Role>>(Array.Empty<Role>());
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(RoleById is not null && RoleById.Id == id ? RoleById : null);
        public Task<Role?> GetByNameAsync(string normalizedName, CancellationToken cancellationToken)
            => Task.FromResult(RoleByName is not null && RoleByName.Name == normalizedName ? RoleByName : null);
        public Task<Role?> GetByIdWithPermissionsAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Role?>(null);
        public Task<Role?> GetByNameWithPermissionsAsync(string normalizedName, CancellationToken cancellationToken) => Task.FromResult<Role?>(null);
        public Task<bool> ExistsByNameAsync(string normalizedName, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> HasActiveAssignmentsAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(Role role, CancellationToken cancellationToken) => Task.CompletedTask;
        public void Update(Role role) { }
        public void Remove(Role role) { }
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class StubClock : IDateTimeProvider
    {
        public StubClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }

    private sealed class StubFeatureFlagService : IFeatureFlagService
    {
        private readonly bool _isEnabled;

        public StubFeatureFlagService(bool isEnabled)
        {
            _isEnabled = isEnabled;
        }

        public int IsEnabledCallCount { get; private set; }

        public Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default)
        {
            IsEnabledCallCount++;
            return Task.FromResult(_isEnabled);
        }

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
}

