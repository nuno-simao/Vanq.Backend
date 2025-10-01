using FluentAssertions;
using Vanq.Domain.Entities;
using Xunit;

namespace Vanq.Infrastructure.Tests.Domain;

public class RoleTests
{
    [Fact]
    public void Create_ShouldNormalizeAndInitializeFields()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var role = Role.Create(" Admin-Team ", "  Administrator  ", "  Full access  ", isSystemRole: true, timestamp);

        // Assert
        role.Id.Should().NotBe(Guid.Empty);
        role.Name.Should().Be("admin-team");
        role.DisplayName.Should().Be("Administrator");
        role.Description.Should().Be("Full access");
        role.IsSystemRole.Should().BeTrue();
        role.SecurityStamp.Should().NotBeNullOrWhiteSpace();
        role.CreatedAt.Should().Be(timestamp);
        role.UpdatedAt.Should().Be(timestamp);
        role.Permissions.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldNullifyEmptyDescription()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var role = Role.Create("viewer", "Viewer", "   ", false, timestamp);

        role.Description.Should().BeNull();
    }

    [Theory]
    [InlineData("" )]
    [InlineData("   ")]
    [InlineData("Invalid Name")]
    [InlineData("1admin")]
    [InlineData("admin@role")]
    public void Create_ShouldThrowWhenNameIsInvalid(string name)
    {
        var timestamp = DateTimeOffset.UtcNow;

        Action act = () => Role.Create(name, "display", null, false, timestamp);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowWhenDisplayNameEmpty()
    {
        var timestamp = DateTimeOffset.UtcNow;

        Action act = () => Role.Create("admin", " ", null, false, timestamp);

        act.Should().Throw<ArgumentException>();
    }
}
