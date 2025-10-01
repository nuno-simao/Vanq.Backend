using Microsoft.Extensions.Options;
using Shouldly;
using Vanq.Infrastructure.Logging;
using Xunit;

namespace Vanq.Infrastructure.Tests.Logging;

public class SensitiveDataRedactorTests
{
    private readonly SensitiveDataRedactor _redactor;

    public SensitiveDataRedactorTests()
    {
        var options = Options.Create(new LoggingOptions
        {
            MaskedFields = ["password", "token", "refreshToken", "email"],
            SensitiveValuePlaceholder = "***"
        });
        _redactor = new SensitiveDataRedactor(options);
    }

    [Fact]
    public void RedactJson_ShouldMaskPasswordField_WhenPasswordIsPresent()
    {
        // Arrange
        var json = """{"email":"user@example.com","password":"secret123"}""";

        // Act
        var redacted = _redactor.RedactJson(json);

        // Assert
        redacted.ShouldContain("\"password\":\"***\"");
        redacted.ShouldNotContain("secret123");
    }

    [Fact]
    public void RedactJson_ShouldMaskEmailField_WhenEmailIsPresent()
    {
        // Arrange
        var json = """{"email":"user@example.com","name":"John"}""";

        // Act
        var redacted = _redactor.RedactJson(json);

        // Assert
        redacted.ShouldContain("\"email\":\"***\"");
        redacted.ShouldNotContain("user@example.com");
    }

    [Fact]
    public void RedactJson_ShouldMaskTokenField_WhenTokenIsPresent()
    {
        // Arrange
        var json = """{"token":"eyJhbGciOiJIUzI1NiIs","userId":"123"}""";

        // Act
        var redacted = _redactor.RedactJson(json);

        // Assert
        redacted.ShouldContain("\"token\":\"***\"");
        redacted.ShouldNotContain("eyJhbGciOiJIUzI1NiIs");
    }

    [Fact]
    public void RedactJson_ShouldMaskRefreshTokenField_WhenRefreshTokenIsPresent()
    {
        // Arrange
        var json = """{"refreshToken":"abc123def456","userId":"123"}""";

        // Act
        var redacted = _redactor.RedactJson(json);

        // Assert
        redacted.ShouldContain("\"refreshToken\":\"***\"");
        redacted.ShouldNotContain("abc123def456");
    }

    [Fact]
    public void RedactJson_ShouldPreserveNonSensitiveFields_WhenRedacting()
    {
        // Arrange
        var json = """{"email":"user@example.com","name":"John","age":30}""";

        // Act
        var redacted = _redactor.RedactJson(json);

        // Assert
        redacted.ShouldContain("\"name\":\"John\"");
        redacted.ShouldContain("\"age\":30");
    }

    [Fact]
    public void RedactJson_ShouldHandleNestedObjects_WhenRedacting()
    {
        // Arrange
        var json = """{"user":{"email":"user@example.com","password":"secret"},"sessionId":"xyz"}""";

        // Act
        var redacted = _redactor.RedactJson(json);

        // Assert
        redacted.ShouldContain("\"password\":\"***\"");
        redacted.ShouldContain("\"email\":\"***\"");
        redacted.ShouldContain("\"sessionId\":\"xyz\"");
    }

    [Fact]
    public void RedactJson_ShouldHandleArrays_WhenRedacting()
    {
        // Arrange
        var json = """{"users":[{"email":"user1@example.com"},{"email":"user2@example.com"}]}""";

        // Act
        var redacted = _redactor.RedactJson(json);

        // Assert
        redacted.ShouldNotContain("user1@example.com");
        redacted.ShouldNotContain("user2@example.com");
    }

    [Fact]
    public void RedactPlainText_ShouldMaskEmails_WhenEmailsArePresent()
    {
        // Arrange
        var text = "User john.doe@example.com attempted login";

        // Act
        var redacted = _redactor.RedactPlainText(text);

        // Assert
        redacted.ShouldContain("***");
        redacted.ShouldNotContain("john.doe@example.com");
    }

    [Fact]
    public void RedactPlainText_ShouldMaskCpf_WhenCpfIsPresent()
    {
        // Arrange
        var text = "CPF: 123.456.789-00";

        // Act
        var redacted = _redactor.RedactPlainText(text);

        // Assert
        redacted.ShouldContain("***");
        redacted.ShouldNotContain("123.456.789-00");
    }

    [Fact]
    public void RedactPlainText_ShouldMaskPhone_WhenPhoneIsPresent()
    {
        // Arrange
        var text = "Contact: (11) 98765-4321";

        // Act
        var redacted = _redactor.RedactPlainText(text);

        // Assert
        redacted.ShouldContain("***");
        redacted.ShouldNotContain("98765-4321");
    }

    [Fact]
    public void RedactPlainText_ShouldMaskMultiplePatterns_WhenPresent()
    {
        // Arrange
        var text = "User: user@example.com, CPF: 123.456.789-00, Phone: (11) 98765-4321";

        // Act
        var redacted = _redactor.RedactPlainText(text);

        // Assert
        redacted.ShouldNotContain("user@example.com");
        redacted.ShouldNotContain("123.456.789-00");
        redacted.ShouldNotContain("98765-4321");
    }

    [Fact]
    public void ShouldRedactField_ShouldReturnTrue_WhenFieldIsInMaskedList()
    {
        // Act & Assert
        _redactor.ShouldRedactField("password").ShouldBeTrue();
        _redactor.ShouldRedactField("token").ShouldBeTrue();
        _redactor.ShouldRedactField("email").ShouldBeTrue();
    }

    [Fact]
    public void ShouldRedactField_ShouldReturnFalse_WhenFieldIsNotInMaskedList()
    {
        // Act & Assert
        _redactor.ShouldRedactField("name").ShouldBeFalse();
        _redactor.ShouldRedactField("age").ShouldBeFalse();
        _redactor.ShouldRedactField("userId").ShouldBeFalse();
    }

    [Fact]
    public void ShouldRedactField_ShouldBeCaseInsensitive_WhenChecking()
    {
        // Act & Assert
        _redactor.ShouldRedactField("PASSWORD").ShouldBeTrue();
        _redactor.ShouldRedactField("Token").ShouldBeTrue();
        _redactor.ShouldRedactField("EMAIL").ShouldBeTrue();
    }

    [Fact]
    public void RedactJson_ShouldFallbackToPlainTextRedaction_WhenJsonIsInvalid()
    {
        // Arrange
        var invalidJson = "Not a JSON string with email: user@example.com";

        // Act
        var redacted = _redactor.RedactJson(invalidJson);

        // Assert
        redacted.ShouldNotContain("user@example.com");
        redacted.ShouldContain("***");
    }
}
