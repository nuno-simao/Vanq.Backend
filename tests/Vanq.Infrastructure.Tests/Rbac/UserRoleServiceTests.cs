using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        var featureManager = new StubFeatureManager();
        var clock = new StubClock(DateTime.UtcNow);
        var options = Options.Create(new RbacOptions { DefaultRole = "viewer" });
        var service = CreateService(userRepository, roleRepository, unitOfWork, clock, featureManager, options);

        // Act
        await service.AssignRoleAsync(user.Id, role.Id, executorId, CancellationToken.None);

        // Assert
        featureManager.EnsureCalled.Should().BeTrue();
        userRepository.UpdateCallCount.Should().Be(1);
        unitOfWork.SaveChangesCallCount.Should().Be(1);
        user.HasActiveRole(role.Id).Should().BeTrue();
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
        var featureManager = new StubFeatureManager();
        var clock = new StubClock(DateTime.UtcNow);
        var options = Options.Create(new RbacOptions { DefaultRole = "viewer" });
        var service = CreateService(userRepository, roleRepository, unitOfWork, clock, featureManager, options);

        await service.AssignRoleAsync(user.Id, role.Id, executorId, CancellationToken.None);

        userRepository.UpdateCallCount.Should().Be(0);
        unitOfWork.SaveChangesCallCount.Should().Be(0);
        user.Roles.Count(roleAssignment => roleAssignment.RoleId == role.Id && roleAssignment.IsActive)
            .Should().Be(1);
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
        var featureManager = new StubFeatureManager();
        var clock = new StubClock(timestamp);
        var options = Options.Create(new RbacOptions { DefaultRole = defaultRole.Name });
        var service = CreateService(userRepository, roleRepository, unitOfWork, clock, featureManager, options);

        await service.RevokeRoleAsync(user.Id, primaryRole.Id, executorId, CancellationToken.None);

        user.HasActiveRole(primaryRole.Id).Should().BeFalse();
        user.HasActiveRole(defaultRole.Id).Should().BeTrue();
        userRepository.UpdateCallCount.Should().Be(1);
        unitOfWork.SaveChangesCallCount.Should().Be(1);
    }

    private static UserRoleService CreateService(
        StubUserRepository userRepository,
        StubRoleRepository roleRepository,
        StubUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        StubFeatureManager featureManager,
        IOptions<RbacOptions> options)
    {
        return new UserRoleService(
            userRepository,
            roleRepository,
            unitOfWork,
            clock,
            featureManager,
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

    private sealed class StubFeatureManager : IRbacFeatureManager
    {
        public bool EnsureCalled { get; private set; }

        public bool IsEnabled => true;

        public Task EnsureEnabledAsync(CancellationToken cancellationToken)
        {
            EnsureCalled = true;
            return Task.CompletedTask;
        }
    }
}
