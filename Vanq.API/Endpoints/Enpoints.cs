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
        
        return app;
    }
}