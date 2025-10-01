using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vanq.API.Authorization;

public static class PermissionEndpointExtensions
{
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        builder.AddEndpointFilterFactory((factoryContext, next) =>
        {
            var loggerFactory = factoryContext.ApplicationServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<PermissionEndpointFilter>();
            var filter = new PermissionEndpointFilter(permission, logger);
            return invocationContext => filter.InvokeAsync(invocationContext, next);
        });

        return builder;
    }
}
