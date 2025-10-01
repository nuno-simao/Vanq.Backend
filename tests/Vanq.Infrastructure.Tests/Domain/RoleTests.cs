using Shouldly;
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
        role.Id.ShouldNotBe(Guid.Empty);
        role.Name.ShouldBe("admin-team");
        role.DisplayName.ShouldBe("Administrator");
        role.Description.ShouldBe("Full access");
        role.IsSystemRole.ShouldBeTrue();
        role.SecurityStamp.ShouldNotBeNullOrWhiteSpace();
        role.CreatedAt.ShouldBe(timestamp);
        role.UpdatedAt.ShouldBe(timestamp);
        role.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public void Create_ShouldNullifyEmptyDescription()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var role = Role.Create("viewer", "Viewer", "   ", false, timestamp);

        role.Description.ShouldBeNull();
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

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void Create_ShouldThrowWhenDisplayNameEmpty()
    {
        var timestamp = DateTimeOffset.UtcNow;

        Action act = () => Role.Create("admin", " ", null, false, timestamp);

        Should.Throw<ArgumentException>(act);
    }
}

