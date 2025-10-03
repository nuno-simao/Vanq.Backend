using Vanq.Shared.Validation;

namespace Vanq.Domain.Entities;

public class SystemParameter
{
    public Guid Id { get; private set; }
    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;
    public string Type { get; private set; } = null!;
    public string? Category { get; private set; }
    public bool IsSensitive { get; private set; }
    public string? LastUpdatedBy { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }
    public string? Reason { get; private set; }
    public string? Metadata { get; private set; }

    private SystemParameter() { }

    private SystemParameter(
        Guid id,
        string key,
        string value,
        string type,
        string? category,
        bool isSensitive,
        string? lastUpdatedBy,
        DateTime lastUpdatedAt,
        string? reason,
        string? metadata)
    {
        Id = id;
        Key = key;
        Value = value;
        Type = type;
        Category = category;
        IsSensitive = isSensitive;
        LastUpdatedBy = lastUpdatedBy;
        LastUpdatedAt = lastUpdatedAt;
        Reason = reason;
        Metadata = metadata;
    }

    public static SystemParameter Create(
        string key,
        string value,
        string type,
        string? category,
        bool isSensitive,
        string? createdBy,
        DateTime nowUtc,
        string? reason = null,
        string? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        SystemParameterKeyValidator.Validate(key);
        ValidateType(type);

        if (!string.IsNullOrWhiteSpace(category) && category.Length > 64)
            throw new ArgumentException("Category cannot exceed 64 characters", nameof(category));

        if (!string.IsNullOrWhiteSpace(reason) && reason.Length > 256)
            throw new ArgumentException("Reason cannot exceed 256 characters", nameof(reason));

        var normalizedKey = key.ToLowerInvariant();

        return new SystemParameter(
            Guid.NewGuid(),
            normalizedKey,
            value,
            type.ToLowerInvariant(),
            category?.Trim(),
            isSensitive,
            createdBy?.Trim(),
            nowUtc,
            reason?.Trim(),
            metadata?.Trim());
    }

    public void Update(
        string value,
        string? updatedBy,
        DateTime nowUtc,
        string? reason = null,
        string? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!string.IsNullOrWhiteSpace(reason) && reason.Length > 256)
            throw new ArgumentException("Reason cannot exceed 256 characters", nameof(reason));

        Value = value;
        LastUpdatedBy = updatedBy?.Trim();
        LastUpdatedAt = nowUtc;
        Reason = reason?.Trim();

        if (metadata != null)
            Metadata = metadata.Trim();
    }

    public void MarkAsSensitive()
    {
        IsSensitive = true;
    }

    public void MarkAsNonSensitive()
    {
        IsSensitive = false;
    }

    private static void ValidateType(string type)
    {
        var validTypes = new[] { "string", "int", "decimal", "bool", "json" };
        if (!validTypes.Contains(type.ToLowerInvariant()))
        {
            throw new ArgumentException(
                $"Invalid type '{type}'. Must be one of: {string.Join(", ", validTypes)}",
                nameof(type));
        }
    }
}
