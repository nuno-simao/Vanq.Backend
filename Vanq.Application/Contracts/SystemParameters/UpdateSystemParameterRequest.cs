namespace Vanq.Application.Contracts.SystemParameters;

public sealed record UpdateSystemParameterRequest(
    string Value,
    string? Reason = null,
    string? Metadata = null);
