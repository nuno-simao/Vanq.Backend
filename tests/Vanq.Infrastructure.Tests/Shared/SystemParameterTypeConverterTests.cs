using Shouldly;
using Vanq.Shared;
using Xunit;

namespace Vanq.Infrastructure.Tests.Shared;

public class SystemParameterTypeConverterTests
{
    [Fact]
    public void ConvertTo_ShouldConvertString()
    {
        // Arrange
        var value = "test value";

        // Act
        var result = SystemParameterTypeConverter.ConvertTo<string>(value, "string");

        // Assert
        result.ShouldBe("test value");
    }

    [Theory]
    [InlineData("123", 123)]
    [InlineData("0", 0)]
    [InlineData("-456", -456)]
    public void ConvertTo_ShouldConvertInt(string value, int expected)
    {
        // Act
        var result = SystemParameterTypeConverter.ConvertTo<int>(value, "int");

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("123.45", 123.45)]
    [InlineData("0.0", 0.0)]
    [InlineData("-456.789", -456.789)]
    public void ConvertTo_ShouldConvertDecimal(string value, double expected)
    {
        // Act
        var result = SystemParameterTypeConverter.ConvertTo<decimal>(value, "decimal");

        // Assert
        result.ShouldBe((decimal)expected);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", true)]
    [InlineData("False", false)]
    public void ConvertTo_ShouldConvertBool(string value, bool expected)
    {
        // Act
        var result = SystemParameterTypeConverter.ConvertTo<bool>(value, "bool");

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void ConvertTo_ShouldConvertJson()
    {
        // Arrange
        var jsonValue = "{\"Name\":\"test\",\"Value\":123}";

        // Act
        var result = SystemParameterTypeConverter.ConvertTo<TestModel>(jsonValue, "json");

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("test");
        result.Value.ShouldBe(123);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123.45")]
    [InlineData("")]
    public void ConvertTo_ShouldThrowForInvalidInt(string value)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            SystemParameterTypeConverter.ConvertTo<int>(value, "int"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("yes")]
    [InlineData("1")]
    public void ConvertTo_ShouldThrowForInvalidBool(string value)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            SystemParameterTypeConverter.ConvertTo<bool>(value, "bool"));
    }

    [Fact]
    public void ConvertTo_ShouldThrowForInvalidJson()
    {
        // Arrange
        var invalidJson = "{invalid json";

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            SystemParameterTypeConverter.ConvertTo<TestModel>(invalidJson, "json"));
    }

    [Fact]
    public void ConvertTo_ShouldThrowForUnsupportedType()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            SystemParameterTypeConverter.ConvertTo<string>("value", "unsupported"));
    }

    [Theory]
    [InlineData("test", "string", true)]
    [InlineData("123", "int", true)]
    [InlineData("123.45", "decimal", true)]
    [InlineData("true", "bool", true)]
    [InlineData("{\"key\":\"value\"}", "json", true)]
    [InlineData("abc", "int", false)]
    [InlineData("yes", "bool", false)]
    [InlineData("{invalid", "json", false)]
    public void CanConvert_ShouldReturnCorrectResult(string value, string type, bool expected)
    {
        // Act
        var result = SystemParameterTypeConverter.CanConvert(value, type);

        // Assert
        result.ShouldBe(expected);
    }

    public class TestModel
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
