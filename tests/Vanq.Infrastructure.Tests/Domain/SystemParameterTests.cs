using Shouldly;
using Vanq.Domain.Entities;
using Xunit;

namespace Vanq.Infrastructure.Tests.Domain;

public class SystemParameterTests
{
    private readonly DateTime _testDate = new(2025, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ShouldNormalizeKeyAndInitializeFields()
    {
        // Arrange & Act
        var parameter = SystemParameter.Create(
            key: "auth.password.min-length",
            value: "8",
            type: "int",
            category: "auth",
            isSensitive: false,
            createdBy: "admin@example.com",
            nowUtc: _testDate);

        // Assert
        parameter.Id.ShouldNotBe(Guid.Empty);
        parameter.Key.ShouldBe("auth.password.min-length");
        parameter.Value.ShouldBe("8");
        parameter.Type.ShouldBe("int");
        parameter.Category.ShouldBe("auth");
        parameter.IsSensitive.ShouldBeFalse();
        parameter.LastUpdatedBy.ShouldBe("admin@example.com");
        parameter.LastUpdatedAt.ShouldBe(_testDate);
    }

    [Theory]
    [InlineData("auth.password.min")]
    [InlineData("auth.password.min.length")]
    [InlineData("auth.password.min.length.value")]
    public void Create_ShouldAcceptValidDotCaseKeys(string key)
    {
        // Act
        var parameter = SystemParameter.Create(key, "value", "string", null, false, "admin", _testDate);

        // Assert
        parameter.Key.ShouldBe(key.ToLowerInvariant());
    }

    [Theory]
    [InlineData("auth")]
    [InlineData("auth.password")]
    [InlineData("auth.password.min.length.value.extra")]
    [InlineData("auth..password")]
    [InlineData("Auth_Password")]
    [InlineData("1auth.password.min")]
    public void Create_ShouldThrowWhenKeyIsInvalid(string key)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            SystemParameter.Create(key, "value", "string", null, false, "admin", _testDate));
    }

    [Theory]
    [InlineData("string")]
    [InlineData("int")]
    [InlineData("decimal")]
    [InlineData("bool")]
    [InlineData("json")]
    public void Create_ShouldAcceptValidTypes(string type)
    {
        // Act
        var parameter = SystemParameter.Create("auth.test.key", "value", type, null, false, "admin", _testDate);

        // Assert
        parameter.Type.ShouldBe(type.ToLowerInvariant());
    }

    [Theory]
    [InlineData("integer")]
    [InlineData("number")]
    [InlineData("float")]
    [InlineData("array")]
    public void Create_ShouldThrowWhenTypeIsInvalid(string type)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            SystemParameter.Create("auth.test.key", "value", type, null, false, "admin", _testDate));
    }

    [Fact]
    public void Update_ShouldModifyValueAndMetadata()
    {
        // Arrange
        var parameter = SystemParameter.Create("auth.test.key", "old-value", "string", null, false, "admin", _testDate);
        var updateDate = _testDate.AddMinutes(5);

        // Act
        parameter.Update("new-value", "editor@example.com", updateDate, "Configuration change", "{\"version\": 2}");

        // Assert
        parameter.Value.ShouldBe("new-value");
        parameter.LastUpdatedBy.ShouldBe("editor@example.com");
        parameter.LastUpdatedAt.ShouldBe(updateDate);
        parameter.Reason.ShouldBe("Configuration change");
        parameter.Metadata.ShouldBe("{\"version\": 2}");
    }

    [Fact]
    public void Update_ShouldThrowWhenReasonTooLong()
    {
        // Arrange
        var parameter = SystemParameter.Create("auth.test.key", "value", "string", null, false, "admin", _testDate);
        var longReason = new string('x', 257);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            parameter.Update("new-value", "admin", _testDate, longReason));
    }

    [Fact]
    public void MarkAsSensitive_ShouldSetFlag()
    {
        // Arrange
        var parameter = SystemParameter.Create("auth.secret.key", "secret", "string", null, false, "admin", _testDate);

        // Act
        parameter.MarkAsSensitive();

        // Assert
        parameter.IsSensitive.ShouldBeTrue();
    }

    [Fact]
    public void MarkAsNonSensitive_ShouldClearFlag()
    {
        // Arrange
        var parameter = SystemParameter.Create("auth.secret.key", "secret", "string", null, true, "admin", _testDate);

        // Act
        parameter.MarkAsNonSensitive();

        // Assert
        parameter.IsSensitive.ShouldBeFalse();
    }

    [Fact]
    public void Create_ShouldNormalizeKeyAndTrimCategory()
    {
        // Act
        var parameter = SystemParameter.Create(
            key: "auth.test.key",
            value: "value",
            type: "string",
            category: "  auth  ",
            isSensitive: false,
            createdBy: "  admin  ",
            nowUtc: _testDate);

        // Assert
        parameter.Key.ShouldBe("auth.test.key");
        parameter.Category.ShouldBe("auth");
        parameter.LastUpdatedBy.ShouldBe("admin");
    }
}
