using FluentAssertions;
using Vanq.Domain.Entities;
using Xunit;

namespace Vanq.Infrastructure.Tests.Domain;

public class PermissionTests
{
    [Fact]
    public void Create_ShouldNormalizeAndInitializeFields()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var permission = Permission.Create(" Admin:Dashboard:View ", "  View Dashboard  ", "  Allows viewing  ", timestamp);

        permission.Id.Should().NotBe(Guid.Empty);
        permission.Name.Should().Be("admin:dashboard:view");
        permission.DisplayName.Should().Be("View Dashboard");
        permission.Description.Should().Be("Allows viewing");
        permission.CreatedAt.Should().Be(timestamp);
    }

    [Fact]
    public void Create_ShouldNullifyEmptyDescription()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var permission = Permission.Create("analytics:report:view", "View Report", "   ", timestamp);

        permission.Description.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("analytics")]
    [InlineData("analytics:report")]
    [InlineData("analytics:report:view-extra:invalid segment")]
    public void Create_ShouldThrowWhenNameInvalid(string name)
    {
        var timestamp = DateTimeOffset.UtcNow;

        Action act = () => Permission.Create(name, "display", null, timestamp);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowWhenDisplayNameEmpty()
    {
        var timestamp = DateTimeOffset.UtcNow;

        Action act = () => Permission.Create("analytics:report:view", " ", null, timestamp);

        act.Should().Throw<ArgumentException>();
    }
}
