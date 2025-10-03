using Shouldly;
using Vanq.Domain.Exceptions;
using Xunit;

namespace Vanq.Infrastructure.Tests.Domain.Exceptions;

public class DomainExceptionTests
{
    [Fact]
    public void ValidationException_ShouldHaveCorrectProperties_WhenCreatedWithField()
    {
        // Arrange & Act
        var exception = new ValidationException("email", "Email is required");

        // Assert
        exception.Message.ShouldBe("Validation failed for field 'email'");
        exception.ErrorCode.ShouldBe("VALIDATION_ERROR");
        exception.HttpStatusCode.ShouldBe(400);
        exception.Errors.ShouldContainKey("email");
        exception.Errors["email"].ShouldBe(new[] { "Email is required" });
    }

    [Fact]
    public void ValidationException_ShouldHaveCorrectProperties_WhenCreatedWithMultipleErrors()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            ["email"] = new[] { "Email is required", "Email is invalid" },
            ["password"] = new[] { "Password must be at least 8 characters" }
        };

        // Act
        var exception = new ValidationException("Multiple validation errors", errors);

        // Assert
        exception.Message.ShouldBe("Multiple validation errors");
        exception.ErrorCode.ShouldBe("VALIDATION_ERROR");
        exception.HttpStatusCode.ShouldBe(400);
        exception.Errors.Count.ShouldBe(2);
        exception.Errors["email"].Length.ShouldBe(2);
        exception.Errors["password"].Length.ShouldBe(1);
    }

    [Fact]
    public void UnauthorizedException_ShouldHaveCorrectProperties_WhenCreated()
    {
        // Arrange & Act
        var exception = new UnauthorizedException("Invalid credentials");

        // Assert
        exception.Message.ShouldBe("Invalid credentials");
        exception.ErrorCode.ShouldBe("UNAUTHORIZED");
        exception.HttpStatusCode.ShouldBe(401);
    }

    [Fact]
    public void UnauthorizedException_ShouldUseDefaultMessage_WhenNotProvided()
    {
        // Arrange & Act
        var exception = new UnauthorizedException();

        // Assert
        exception.Message.ShouldBe("Authentication failed");
        exception.ErrorCode.ShouldBe("UNAUTHORIZED");
        exception.HttpStatusCode.ShouldBe(401);
    }

    [Fact]
    public void ForbiddenException_ShouldHaveCorrectProperties_WhenCreated()
    {
        // Arrange & Act
        var exception = new ForbiddenException("Insufficient permissions");

        // Assert
        exception.Message.ShouldBe("Insufficient permissions");
        exception.ErrorCode.ShouldBe("FORBIDDEN");
        exception.HttpStatusCode.ShouldBe(403);
    }

    [Fact]
    public void ForbiddenException_ShouldUseDefaultMessage_WhenNotProvided()
    {
        // Arrange & Act
        var exception = new ForbiddenException();

        // Assert
        exception.Message.ShouldBe("Access forbidden");
        exception.ErrorCode.ShouldBe("FORBIDDEN");
        exception.HttpStatusCode.ShouldBe(403);
    }

    [Fact]
    public void NotFoundException_ShouldHaveCorrectProperties_WhenCreatedWithResourceInfo()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var exception = new NotFoundException("User", userId);

        // Assert
        exception.Message.ShouldBe($"User with key '{userId}' was not found");
        exception.ErrorCode.ShouldBe("NOT_FOUND");
        exception.HttpStatusCode.ShouldBe(404);
    }

    [Fact]
    public void NotFoundException_ShouldHaveCorrectProperties_WhenCreatedWithMessage()
    {
        // Arrange & Act
        var exception = new NotFoundException("Resource not found");

        // Assert
        exception.Message.ShouldBe("Resource not found");
        exception.ErrorCode.ShouldBe("NOT_FOUND");
        exception.HttpStatusCode.ShouldBe(404);
    }

    [Fact]
    public void ConflictException_ShouldHaveCorrectProperties_WhenCreated()
    {
        // Arrange & Act
        var exception = new ConflictException("Email already exists");

        // Assert
        exception.Message.ShouldBe("Email already exists");
        exception.ErrorCode.ShouldBe("CONFLICT");
        exception.HttpStatusCode.ShouldBe(409);
    }

    [Fact]
    public void DomainException_ShouldSupportCustomErrorCode_WhenProvided()
    {
        // Arrange & Act
        var exception = new ValidationException("Custom error", errorCode: "CUSTOM_ERROR");

        // Assert
        exception.ErrorCode.ShouldBe("CUSTOM_ERROR");
    }
}
