# Logging Guidelines

This document provides guidelines for using structured logging in the Vanq.Backend project.

## Overview

The project uses **Serilog** for structured logging with:
- JSON format for console and file outputs
- Automatic enrichment with TraceId, ThreadId, ProcessId, Environment
- Sensitive data redaction (passwords, tokens, PII)
- Request/response logging middleware
- Helper methods for standardized event logging

## Configuration

Logging is configured in `appsettings.json` under the `StructuredLogging` section:

```json
{
  "StructuredLogging": {
    "MinimumLevel": "Information",
    "MaskedFields": [
      "password",
      "token",
      "refreshToken",
      "email",
      "cpf",
      "telefone",
      "phone"
    ],
    "ConsoleJson": true,
    "FilePath": "logs/vanq-.log",
    "EnableRequestLogging": true,
    "SensitiveValuePlaceholder": "***"
  }
}
```

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `MinimumLevel` | string | `Information` | Minimum log level (Verbose, Debug, Information, Warning, Error, Fatal) |
| `MaskedFields` | string[] | `[]` | List of field names to mask in logs |
| `ConsoleJson` | bool | `true` | Output console logs in JSON format |
| `FilePath` | string | `null` | Path for file logging (supports rolling) |
| `EnableRequestLogging` | bool | `true` | Enable HTTP request/response logging middleware |
| `SensitiveValuePlaceholder` | string | `***` | Replacement value for masked fields |

## Log Levels

Use appropriate log levels for different scenarios:

| Level | When to Use | Examples |
|-------|-------------|----------|
| **Verbose** | Detailed trace information | Internal state dumps, loop iterations |
| **Debug** | Development diagnostics | Query parameters, intermediate values |
| **Information** | Normal application flow | User login, feature flag evaluation, successful operations |
| **Warning** | Recoverable issues | Invalid credentials, missing optional config, retries |
| **Error** | Failures requiring attention | Database errors, external service failures |
| **Fatal** | Application crashes | Unhandled exceptions, startup failures |

## Structured Logging Helpers

The project provides extension methods in `Vanq.Infrastructure.Logging.Extensions.LoggerExtensions` for standardized logging.

### Authentication Events

Use `LogAuthEvent` for authentication-related operations:

```csharp
// Success
_logger.LogAuthEvent(
    eventName: "UserLogin",
    status: "success",
    userId: user.Id,
    email: user.Email
);

// Failure
_logger.LogAuthEvent(
    eventName: "UserLogin",
    status: "failure",
    email: request.Email,
    reason: "InvalidCredentials"
);
```

**Parameters:**
- `eventName`: Event name (e.g., `UserLogin`, `UserRegistration`, `UserLogout`)
- `status`: `success`, `failure`, or `skipped`
- `userId`: Optional user ID (null for anonymous)
- `email`: Optional email (masked automatically)
- `reason`: Optional failure reason

### RBAC Events

Use `LogRbacEvent` for role/permission changes:

```csharp
_logger.LogRbacEvent(
    eventName: "RoleAssigned",
    status: "success",
    targetUserId: userId,
    roleName: "admin",
    actorId: adminId
);
```

**Parameters:**
- `eventName`: Event name (e.g., `RoleAssigned`, `PermissionRevoked`)
- `status`: `success`, `failure`, or `skipped`
- `targetUserId`: User being affected
- `roleName`: Role name (optional)
- `permissionName`: Permission name (optional)
- `actorId`: User performing the action

### Feature Flag Events

Use `LogFeatureFlagEvent` for feature flag evaluations:

```csharp
_logger.LogFeatureFlagEvent(
    flagName: "rbac-enabled",
    isEnabled: true,
    environment: "Development"
);
```

### Security Events

Use `LogSecurityEvent` for suspicious activity:

```csharp
_logger.LogSecurityEvent(
    eventName: "SuspiciousLogin",
    severity: "high",
    ipAddress: "192.168.1.100",
    userId: user.Id,
    details: "Multiple failed login attempts"
);
```

**Severity levels:** `critical`, `high`, `medium`, `low`

### Performance Events

Use `LogPerformanceEvent` for slow operations:

```csharp
_logger.LogPerformanceEvent(
    operationName: "DatabaseQuery",
    elapsedMilliseconds: 1500,
    threshold: 1000 // Logs as warning if exceeded
);
```

### Domain Events

Use `LogDomainEvent` for generic domain operations:

```csharp
_logger.LogDomainEvent(
    eventName: "OrderCreated",
    status: "success",
    properties: new Dictionary<string, object>
    {
        ["OrderId"] = order.Id,
        ["TotalAmount"] = order.Total,
        ["ItemCount"] = order.Items.Count
    }
);
```

## Sensitive Data Protection

### Automatic Masking

Fields configured in `MaskedFields` are automatically masked when logged as JSON:

```csharp
// Original data
var user = new { email = "user@example.com", password = "secret123" };

// Logged output (masked)
{ "email": "***", "password": "***" }
```

### Manual Redaction

Use `SensitiveDataRedactor` for custom redaction:

```csharp
private readonly SensitiveDataRedactor _redactor;

// Inject in constructor
public MyService(SensitiveDataRedactor redactor)
{
    _redactor = redactor;
}

// Redact JSON
var redacted = _redactor.RedactJson(jsonString);

// Redact plain text (emails, CPF, phone numbers)
var redacted = _redactor.RedactPlainText(text);
```

### What to Never Log

**NEVER** log the following in plain text:
- Passwords (hashed or plain)
- JWT tokens
- Refresh tokens
- API keys
- Credit card numbers
- Full CPF/SSN
- Full phone numbers
- Raw email addresses in production

## Best Practices

### 1. Use Structured Properties

✅ **Good:**
```csharp
_logger.LogInformation(
    "User {UserId} created order {OrderId} with total {Amount}",
    userId,
    orderId,
    amount
);
```

❌ **Bad:**
```csharp
_logger.LogInformation($"User {userId} created order {orderId} with total {amount}");
```

### 2. Include Context

Always include relevant context for troubleshooting:
- `UserId` for user-specific operations
- `TraceId` (automatic via Serilog enrichment)
- Entity IDs
- Operation names

### 3. Log Outcomes, Not Inputs

✅ **Good:**
```csharp
_logger.LogAuthEvent("UserLogin", "success", userId: user.Id);
```

❌ **Bad:**
```csharp
_logger.LogInformation("Login request received for {Email} with password {Password}", email, password);
```

### 4. Use Appropriate Levels

- `Information`: Successful operations that should be monitored
- `Warning`: Recoverable failures (retry succeeded, fallback used)
- `Error`: Failures requiring investigation
- `Fatal`: Application-level crashes

### 5. Avoid Logging in Loops

❌ **Bad:**
```csharp
foreach (var item in items)
{
    _logger.LogInformation("Processing {ItemId}", item.Id); // 1000+ log entries
}
```

✅ **Good:**
```csharp
_logger.LogInformation("Processing {Count} items", items.Count);
// Process items
_logger.LogInformation("Completed processing {Count} items", items.Count);
```

### 6. Log Exceptions Properly

✅ **Good:**
```csharp
try
{
    await operation();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to complete {Operation} for user {UserId}", "OrderCreation", userId);
    throw;
}
```

❌ **Bad:**
```csharp
catch (Exception ex)
{
    _logger.LogError(ex.Message); // Loses stack trace
}
```

## Request Logging

The `RequestResponseLoggingMiddleware` automatically logs HTTP requests:

**Logged Properties:**
- HTTP method
- Path
- Status code
- Duration (ms)
- TraceId
- UserId (if authenticated)

**Example Output:**
```json
{
  "@t": "2025-10-01T12:00:00.123Z",
  "@l": "Information",
  "@m": "HTTP POST /auth/login responded 200 in 45ms",
  "Method": "POST",
  "Path": "/auth/login",
  "StatusCode": 200,
  "ElapsedMs": 45,
  "TraceId": "0HN1234567890ABCDEF",
  "UserId": "550e8400-e29b-41d4-a716-446655440000"
}
```

## File Logging

Logs are written to `logs/vanq-YYYYMMDD.log` with:
- Daily rolling
- 30-day retention
- 100MB max file size
- Automatic rotation on size limit
- Compact JSON format

**Example log file entry:**
```json
{"@t":"2025-10-01T12:00:00.123Z","@l":"Information","@mt":"Auth Event: {Event} | Status: {Status}","Event":"UserLogin","Status":"success","UserId":"550e8400-e29b-41d4-a716-446655440000","Application":"Vanq.API","Version":"1.0.0"}
```

## Troubleshooting

### No logs appearing

1. Check `MinimumLevel` in `appsettings.json`
2. Verify Serilog is configured in `Program.cs`
3. Ensure `Log.CloseAndFlush()` is called on shutdown

### Logs missing TraceId

Ensure `Enrich.FromLogContext()` is configured in Serilog setup.

### Sensitive data still visible

1. Add field name to `MaskedFields` in `appsettings.json`
2. Restart application to reload configuration
3. Verify field name matches exactly (case-insensitive)

### File logs not rotating

Check disk space and file permissions on `logs/` directory.

## Examples

### Complete Service Example

```csharp
using Microsoft.Extensions.Logging;
using Vanq.Infrastructure.Logging.Extensions;

public class OrderService
{
    private readonly ILogger<OrderService> _logger;

    public OrderService(ILogger<OrderService> logger)
    {
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        try
        {
            _logger.LogInformation("Creating order for user {UserId} with {ItemCount} items",
                request.UserId, request.Items.Count);

            var order = await ProcessOrderAsync(request);

            _logger.LogDomainEvent("OrderCreated", "success", new Dictionary<string, object>
            {
                ["OrderId"] = order.Id,
                ["UserId"] = request.UserId,
                ["TotalAmount"] = order.Total
            });

            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order for user {UserId}", request.UserId);
            throw;
        }
    }
}
```

## References

- [Serilog Documentation](https://serilog.net/)
- [SPEC-0009: Structured Logging](../specs/SPEC-0009-FEAT-structured-logging.md)
- [CLAUDE.md](../CLAUDE.md#testing-patterns-and-conventions)
