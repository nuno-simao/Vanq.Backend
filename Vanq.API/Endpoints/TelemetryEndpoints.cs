using Microsoft.AspNetCore.Http.HttpResults;

namespace Vanq.API.Endpoints;

public static class TelemetryEndpoints
{
    public static RouteGroupBuilder MapTelemetryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/telemetry")
            .WithTags("Telemetry");

        group.MapPost("/cli", IngestCliTelemetryAsync)
            .WithSummary("Ingest CLI telemetry events (anonymous)")
            .WithDescription("Receives anonymous usage telemetry from Vanq CLI tool")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest)
            .AllowAnonymous();

        return group;
    }

    private static IResult IngestCliTelemetryAsync(
        List<CliTelemetryEvent> events,
        ILogger<Program> logger)
    {
        // Validate
        if (events.Count > 100)
            return Results.BadRequest("Too many events (max 100 per batch)");

        // Log for now (future: store in database or forward to analytics service)
        logger.LogInformation(
            "Received {EventCount} CLI telemetry events from session {SessionId}",
            events.Count,
            events.FirstOrDefault()?.SessionId ?? "unknown"
        );

        foreach (var evt in events)
        {
            logger.LogDebug(
                "CLI Telemetry: Command={Command}, Success={Success}, Duration={Duration}ms, Platform={Platform}",
                evt.CommandName,
                evt.Success,
                evt.DurationMs,
                evt.OsPlatform
            );
        }

        // TODO: Implement actual storage (database, analytics service, etc.)
        // For now, just accept and log

        return Results.Accepted();
    }
}

public record CliTelemetryEvent(
    string AnonymousId,
    string SessionId,
    string CommandName,
    bool Success,
    int DurationMs,
    string? ErrorType,
    string CliVersion,
    string OsPlatform,
    string OsVersion,
    string DotNetVersion,
    DateTime Timestamp,
    string? OutputFormat,
    bool VerboseMode
);
