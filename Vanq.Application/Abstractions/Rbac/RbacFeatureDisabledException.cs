using System;

namespace Vanq.Application.Abstractions.Rbac;

public sealed class RbacFeatureDisabledException : InvalidOperationException
{
    public RbacFeatureDisabledException()
        : base("RBAC feature is disabled.")
    {
    }
}
