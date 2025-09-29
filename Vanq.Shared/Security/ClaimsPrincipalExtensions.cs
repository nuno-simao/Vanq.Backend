using System;
using System.Linq;
using System.Security.Claims;

namespace Vanq.Shared.Security;

public static class ClaimsPrincipalExtensions
{
    private const string SubjectClaimType = "sub";
    private const string EmailClaimType = "email";

    public static bool TryGetUserContext(this ClaimsPrincipal? principal, out Guid userId, out string? email)
    {
        email = null;

        if (principal is null)
        {
            userId = Guid.Empty;
            return false;
        }

        if (!principal.TryGetUserId(out userId))
        {
            return false;
        }

        email = principal.Claims.FirstOrDefault(c => string.Equals(c.Type, EmailClaimType, StringComparison.OrdinalIgnoreCase))?.Value
            ?? principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

        if (string.IsNullOrWhiteSpace(email))
        {
            email = null;
        }

        return email is not null;
    }

    public static bool TryGetUserId(this ClaimsPrincipal? principal, out Guid userId)
    {
        userId = Guid.Empty;

        if (principal is null)
        {
            return false;
        }

        var userIdValue = principal.Claims.FirstOrDefault(c => string.Equals(c.Type, SubjectClaimType, StringComparison.OrdinalIgnoreCase))?.Value
            ?? principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(userIdValue, out userId);
    }
}
