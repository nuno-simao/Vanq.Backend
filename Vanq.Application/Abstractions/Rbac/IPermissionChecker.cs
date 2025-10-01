using System;
using System.Threading;
using System.Threading.Tasks;

namespace Vanq.Application.Abstractions.Rbac;

public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken);

    Task EnsurePermissionAsync(Guid userId, string permission, CancellationToken cancellationToken);
}
