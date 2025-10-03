# CORS Configuration Guide

This document describes how to configure Cross-Origin Resource Sharing (CORS) in Vanq.Backend API.

## Overview

CORS support was implemented according to **SPEC-0002** to allow authorized web clients to consume the API from different origins while maintaining security controls.

## Features

- ✅ Named CORS policy (`vanq-default-cors`)
- ✅ Configurable origins, methods, and headers via `appsettings.json`
- ✅ Environment-specific behavior (relaxed in Development, restricted in Production)
- ✅ Credentials support (Authorization header, cookies)
- ✅ Feature flag support (`cors-relaxed`)
- ✅ Structured logging for blocked requests
- ✅ HTTPS enforcement in Production (BR-01)

## Configuration

### Basic Configuration (`appsettings.json`)

```json
{
  "Cors": {
    "PolicyName": "vanq-default-cors",
    "AllowedOrigins": [
      "https://app.example.com",
      "https://dashboard.example.com"
    ],
    "AllowedMethods": [
      "GET",
      "POST",
      "PUT",
      "PATCH",
      "DELETE",
      "OPTIONS"
    ],
    "AllowedHeaders": [
      "Content-Type",
      "Authorization",
      "Accept",
      "Origin",
      "X-Requested-With"
    ],
    "AllowCredentials": true,
    "MaxAgeSeconds": 3600
  }
}
```

### Configuration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `PolicyName` | string | `"vanq-default-cors"` | Name of the CORS policy to register |
| `AllowedOrigins` | string[] | `[]` | List of allowed origin URLs (must be HTTPS in Production) |
| `AllowedMethods` | string[] | `["GET", "POST", ...]` | HTTP methods allowed for CORS requests |
| `AllowedHeaders` | string[] | `["Content-Type", ...]` | Request headers allowed in CORS requests |
| `AllowCredentials` | bool | `true` | Whether to allow credentials (cookies, Authorization header) |
| `MaxAgeSeconds` | int | `3600` | Preflight cache duration in seconds |

### Environment-Specific Behavior

#### Development Environment

In **Development** mode (`ASPNETCORE_ENVIRONMENT=Development`):

- ✅ Automatically allows **any origin** (`AllowAnyOrigin`)
- ✅ Automatically allows **any method** (`AllowAnyMethod`)
- ✅ Automatically allows **any header** (`AllowAnyHeader`)
- ✅ No need to configure `AllowedOrigins`
- ⚠️ **WARNING:** This is for development convenience only. Never use in Production!

**Example:**
```bash
# Development - no CORS configuration needed
dotnet run --environment Development
```

#### Production/Staging Environment

In **Production** or **Staging** mode:

- ⚠️ **Only configured origins** in `AllowedOrigins` are allowed
- ⚠️ **HTTPS is enforced** (BR-01): HTTP origins are automatically filtered out
- ⚠️ **Credentials require specific origins** (BR-03): Cannot use `AllowAnyOrigin` with `AllowCredentials`

**Example Production Configuration:**

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://app.example.com",
      "https://dashboard.example.com"
    ],
    "AllowCredentials": true
  }
}
```

### Environment Variables

You can override configuration using environment variables:

```bash
# Windows PowerShell
$env:Cors__AllowedOrigins__0="https://app.example.com"
$env:Cors__AllowedOrigins__1="https://dashboard.example.com"
$env:Cors__AllowCredentials="true"

# Linux/macOS
export Cors__AllowedOrigins__0="https://app.example.com"
export Cors__AllowedOrigins__1="https://dashboard.example.com"
export Cors__AllowCredentials="true"
```

## Feature Flags

### FLAG-01: `cors-relaxed`

Enables a relaxed CORS policy (allow any origin) even in non-Development environments.

**Use cases:**
- Temporary testing in Staging
- Emergency override for production issues
- Gradual rollout testing

**Default:** `false` (disabled)

**How to enable:**

1. Via database (recommended):
   ```http
   PATCH /api/admin/feature-flags/cors-relaxed
   Authorization: Bearer {admin-token}
   Content-Type: application/json

   {
     "isEnabled": true
   }
   ```

2. Via feature flag service:
   ```csharp
   await featureFlagService.UpdateFlagAsync("cors-relaxed", true);
   ```

**⚠️ Security Warning:**
When `cors-relaxed` is enabled:
- Any origin can access the API
- Use only for debugging or temporary scenarios
- Monitor logs for suspicious activity
- Disable as soon as possible

## Business Rules

### BR-01: HTTPS Enforcement in Production

Only HTTPS origins are allowed in Production environment.

**Valid:**
```json
{
  "AllowedOrigins": [
    "https://app.example.com",
    "https://dashboard.example.com"
  ]
}
```

**Invalid (will be filtered out):**
```json
{
  "AllowedOrigins": [
    "http://app.example.com",  // ❌ HTTP not allowed in Production
    "https://dashboard.example.com"
  ]
}
```

### BR-02: Origin Comparison

Origin comparison is **case-insensitive** and ignores trailing slashes.

**Example:**
- `https://App.Example.com/` → normalized to → `https://app.example.com`

### BR-03: AllowCredentials Restriction

When `AllowCredentials` is `true`, you **cannot** use `AllowAnyOrigin`.

**Valid:**
```json
{
  "AllowedOrigins": ["https://app.example.com"],
  "AllowCredentials": true  // ✅ OK - specific origin
}
```

**Invalid:**
```json
{
  "AllowedOrigins": ["*"],
  "AllowCredentials": true  // ❌ Error - violates BR-03
}
```

## Logging and Observability

### Structured Logging (NFR-02)

CORS requests are logged with structured data for observability:

#### Blocked Request Log

```json
{
  "level": "Warning",
  "event": "cors-blocked",
  "origin": "https://unauthorized.com",
  "path": "/auth/login",
  "method": "POST",
  "statusCode": 403,
  "duration": 45
}
```

#### Allowed Request Log (Debug)

```json
{
  "level": "Debug",
  "event": "cors-allowed",
  "origin": "https://app.example.com",
  "path": "/auth/login",
  "method": "POST",
  "duration": 23
}
```

#### Slow Preflight Warning (NFR-03)

```json
{
  "level": "Warning",
  "event": "cors-preflight-slow",
  "origin": "https://app.example.com",
  "path": "/auth/login",
  "duration": 150,
  "threshold": 120
}
```

### Performance Monitoring

**NFR-03:** Preflight requests should respond in **p95 < 120ms**.

If preflight responses exceed 120ms, a warning is logged for investigation.

## Testing

### Manual Testing with cURL

#### Preflight Request (OPTIONS)

```bash
curl -X OPTIONS https://localhost:5001/auth/login \
  -H "Origin: https://example.com" \
  -H "Access-Control-Request-Method: POST" \
  -H "Access-Control-Request-Headers: Content-Type,Authorization" \
  -v
```

**Expected Response Headers:**
```
HTTP/1.1 204 No Content
Access-Control-Allow-Origin: https://example.com
Access-Control-Allow-Methods: GET, POST, PUT, PATCH, DELETE, OPTIONS
Access-Control-Allow-Headers: Content-Type, Authorization, ...
Access-Control-Allow-Credentials: true
Access-Control-Max-Age: 3600
```

#### Actual Request (POST)

```bash
curl -X POST https://localhost:5001/auth/login \
  -H "Origin: https://example.com" \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password"}' \
  -v
```

**Expected Response Headers:**
```
HTTP/1.1 200 OK
Access-Control-Allow-Origin: https://example.com
Access-Control-Allow-Credentials: true
```

### Automated Tests

Run integration tests:

```bash
dotnet test tests/Vanq.API.Tests/Cors/CorsIntegrationTests.cs
dotnet test tests/Vanq.API.Tests/Cors/CorsConfigurationTests.cs
```

## Troubleshooting

### Problem: CORS errors in browser console

**Symptom:**
```
Access to fetch at 'https://api.example.com/auth/login' from origin 'https://app.example.com'
has been blocked by CORS policy: No 'Access-Control-Allow-Origin' header is present.
```

**Solutions:**

1. **Check origin is configured:**
   ```json
   {
     "Cors": {
       "AllowedOrigins": ["https://app.example.com"]
     }
   }
   ```

2. **Verify environment:**
   ```bash
   # Should show "Development" for local testing
   echo $ASPNETCORE_ENVIRONMENT
   ```

3. **Check logs for blocked requests:**
   ```bash
   grep "cors-blocked" logs/vanq-*.log
   ```

4. **Ensure HTTPS in Production:**
   - ❌ `http://app.example.com` (will be filtered out)
   - ✅ `https://app.example.com`

### Problem: Preflight request fails (OPTIONS returns 404)

**Cause:** CORS middleware not registered or in wrong order.

**Solution:** Verify middleware order in `Program.cs`:

```csharp
app.UseHttpsRedirection();
app.UseVanqCors(builder.Configuration, builder.Environment);  // Before Authentication
app.UseCorsLogging();
app.UseAuthentication();
app.UseAuthorization();
```

### Problem: Credentials not being sent

**Symptom:** Frontend fetch fails to send Authorization header or cookies.

**Solutions:**

1. **Configure frontend fetch:**
   ```javascript
   fetch('https://api.example.com/auth/me', {
     credentials: 'include',  // Required for cookies
     headers: {
       'Authorization': `Bearer ${token}`
     }
   })
   ```

2. **Verify AllowCredentials is enabled:**
   ```json
   {
     "Cors": {
       "AllowCredentials": true
     }
   }
   ```

3. **Check specific origin (not wildcard):**
   - ❌ `AllowedOrigins: ["*"]` with `AllowCredentials: true` (invalid)
   - ✅ `AllowedOrigins: ["https://app.example.com"]` with `AllowCredentials: true`

### Problem: Slow preflight responses (> 120ms)

**Cause:** Heavy middleware running before CORS.

**Solution:** Move `UseVanqCors` earlier in pipeline:

```csharp
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();
app.UseSerilogRequestLogging();
// ... OpenAPI ...
app.UseHttpsRedirection();
app.UseVanqCors(...);  // ← Should be here, before authentication
app.UseAuthentication();
```

## Security Best Practices

1. ⚠️ **Never use `AllowAnyOrigin` in Production**
   - Development only
   - Use specific origins in Production/Staging

2. ⚠️ **Always use HTTPS origins in Production** (BR-01)
   - HTTP origins are automatically filtered out
   - Configure SSL/TLS certificates properly

3. ⚠️ **Minimize allowed origins**
   - Only add trusted domains
   - Remove unused origins regularly

4. ⚠️ **Monitor blocked requests** (NFR-02)
   - Review `cors-blocked` logs regularly
   - Investigate suspicious patterns

5. ⚠️ **Use feature flag `cors-relaxed` sparingly**
   - Temporary testing only
   - Disable immediately after use
   - Require approval for Production

6. ⚠️ **Keep credentials enabled only when needed**
   - Required for Authorization headers
   - Required for cookie-based sessions
   - Disable if not using authentication

## Future Enhancements (Out of Scope for SPEC-0002)

- ❌ Dynamic configuration per client/tenant
- ❌ Admin UI for managing origins
- ❌ Distributed cache for preflight responses
- ❌ Advanced metrics (will be added in SPEC-0010)

## References

- **SPEC-0002:** CORS Support specification
- **RFC 6454:** The Web Origin Concept
- **MDN CORS:** https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS
- **ASP.NET Core CORS:** https://learn.microsoft.com/en-us/aspnet/core/security/cors

## Support

For issues or questions:
- Check logs: `logs/vanq-*.log`
- Review this documentation
- Check SPEC-0002 specification
- Contact: [GitHub Issues](https://github.com/vanq/backend/issues)
