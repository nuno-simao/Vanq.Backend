using System.Threading;
using System.Threading.Tasks;

namespace Vanq.Application.Abstractions.Rbac;

public interface IRbacFeatureManager
{
    bool IsEnabled { get; }

    Task EnsureEnabledAsync(CancellationToken cancellationToken);
}
