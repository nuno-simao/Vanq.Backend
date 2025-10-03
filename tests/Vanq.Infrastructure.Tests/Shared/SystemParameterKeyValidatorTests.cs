using Shouldly;
using Vanq.Shared.Validation;
using Xunit;

namespace Vanq.Infrastructure.Tests.Shared;

public class SystemParameterKeyValidatorTests
{
    [Theory]
    [InlineData("auth.password.min")]
    [InlineData("auth.password.min.length")]
    [InlineData("auth.password.min.length.value")]
    [InlineData("system.email.smtp.port")]
    [InlineData("api.rate-limit.requests-per-minute")]
    public void Validate_ShouldAcceptValidKeys(string key)
    {
        // Act & Assert
        Should.NotThrow(() => SystemParameterKeyValidator.Validate(key));
    }

    [Theory]
    [InlineData("auth")]
    [InlineData("auth.password")]
    [InlineData("auth.password.min.length.value.extra")]
    [InlineData("Auth.Password.Min")]
    [InlineData("auth..password")]
    [InlineData(".auth.password")]
    [InlineData("auth.password.")]
    [InlineData("1auth.password.min")]
    [InlineData("auth_password_min")]
    public void Validate_ShouldThrowForInvalidKeys(string key)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => SystemParameterKeyValidator.Validate(key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldThrowForEmptyKeys(string key)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => SystemParameterKeyValidator.Validate(key));
    }

    [Fact]
    public void Validate_ShouldThrowForNullKey()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => SystemParameterKeyValidator.Validate(null!));
    }

    [Fact]
    public void Validate_ShouldThrowForTooLongKey()
    {
        // Arrange
        var longKey = new string('a', 140) + "." + new string('b', 10) + ".c";

        // Act & Assert
        Should.Throw<ArgumentException>(() => SystemParameterKeyValidator.Validate(longKey));
    }

    [Theory]
    [InlineData("auth.password.min", true)]
    [InlineData("auth.password.min.length", true)]
    [InlineData("auth.password", false)]
    [InlineData("auth", false)]
    [InlineData("Auth.Password.Min", false)]
    [InlineData("auth..password.min", false)]
    public void IsValid_ShouldReturnCorrectResult(string key, bool expected)
    {
        // Act
        var result = SystemParameterKeyValidator.IsValid(key);

        // Assert
        result.ShouldBe(expected);
    }
}
