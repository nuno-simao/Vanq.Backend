namespace Vanq.Application.Contracts.SystemParameters;

public sealed record SystemParameterDto(
    Guid Id,
    string Key,
    string Value,
    string Type,
    string? Category,
    bool IsSensitive,
    string? LastUpdatedBy,
    DateTime LastUpdatedAt,
    string? Reason,
    string? Metadata);
