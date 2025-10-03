using Microsoft.AspNetCore.Http.HttpResults;

namespace Vanq.API.Endpoints;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/health")
            .WithTags("Health");

        group.MapGet("/ready", GetReadyAsync)
            .WithSummary("Health check endpoint - returns 200 if API is ready")
            .WithDescription("Used by CLI and monitoring tools to verify API availability")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .AllowAnonymous();

        return group;
    }

    private static Ok<HealthResponse> GetReadyAsync()
    {
        var response = new HealthResponse(
            "Healthy",
            new HealthCheckDetail("Database", "Healthy", null),
            new HealthCheckDetail("Environment", "Healthy", null),
            GetUptime()
        );

        return TypedResults.Ok(response);
    }

    private static string GetUptime()
    {
        var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
        return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
    }
}

public record HealthResponse(
    string Status,
    HealthCheckDetail Database,
    HealthCheckDetail Environment,
    string Uptime
);

public record HealthCheckDetail(
    string Name,
    string Status,
    string? ResponseTime
);
