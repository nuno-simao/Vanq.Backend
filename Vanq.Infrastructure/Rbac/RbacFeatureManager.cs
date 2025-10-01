using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vanq.Application.Abstractions.Rbac;
using Vanq.Application.Configuration;

namespace Vanq.Infrastructure.Rbac;

internal sealed class RbacFeatureManager : IRbacFeatureManager
{
    private readonly IOptionsMonitor<RbacOptions> _options;
    private readonly ILogger<RbacFeatureManager> _logger;

    public RbacFeatureManager(IOptionsMonitor<RbacOptions> options, ILogger<RbacFeatureManager> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsEnabled => _options.CurrentValue.FeatureEnabled;

    public Task EnsureEnabledAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            _logger.LogWarning("RBAC feature flag is disabled. Access blocked.");
            throw new RbacFeatureDisabledException();
        }

        return Task.CompletedTask;
    }
}
