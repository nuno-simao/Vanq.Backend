using Shouldly;
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

        permission.Id.ShouldNotBe(Guid.Empty);
        permission.Name.ShouldBe("admin:dashboard:view");
        permission.DisplayName.ShouldBe("View Dashboard");
        permission.Description.ShouldBe("Allows viewing");
        permission.CreatedAt.ShouldBe(timestamp);
    }

    [Fact]
    public void Create_ShouldNullifyEmptyDescription()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var permission = Permission.Create("analytics:report:view", "View Report", "   ", timestamp);

        permission.Description.ShouldBeNull();
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

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void Create_ShouldThrowWhenDisplayNameEmpty()
    {
        var timestamp = DateTimeOffset.UtcNow;

        Action act = () => Permission.Create("analytics:report:view", " ", null, timestamp);

        Should.Throw<ArgumentException>(act);
    }
}

