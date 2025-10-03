namespace Vanq.Application.Contracts.SystemParameters;

public sealed record CreateSystemParameterRequest(
    string Key,
    string Value,
    string Type,
    string? Category = null,
    bool IsSensitive = false,
    string? Reason = null,
    string? Metadata = null);
