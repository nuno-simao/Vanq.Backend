namespace Vanq.API.Endpoints;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapAllEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder apiRoute = app.MapGroup("/api").WithTags("API");

        // Authentication management
        apiRoute.MapAuthEndpoints();

        // Feature flags management
        apiRoute.MapFeatureFlagsEndpoints();

        // System parameters management
        apiRoute.MapSystemParametersEndpoints();

        // Health checks (outside /api group)
        app.MapHealthEndpoints();

        // Telemetry ingestion
        app.MapTelemetryEndpoints();

        return app;
    }
}