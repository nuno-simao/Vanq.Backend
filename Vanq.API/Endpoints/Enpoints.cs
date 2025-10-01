namespace Vanq.API.Endpoints;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapAllEndpoints(this IEndpointRouteBuilder app)
    {
        return app.MapAuthEndpoints();
    }
}