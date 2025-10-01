using System;
using System.Threading;
using System.Threading.Tasks;

namespace Vanq.Application.Abstractions.Rbac;

public interface IUserRoleService
{
    Task AssignRoleAsync(Guid userId, Guid roleId, Guid executorId, CancellationToken cancellationToken);

    Task RevokeRoleAsync(Guid userId, Guid roleId, Guid executorId, CancellationToken cancellationToken);
}
