using System.Globalization;
using System.Text.Json;

namespace Vanq.Shared;

/// <summary>
/// Provides type conversion utilities for system parameters.
/// Converts string values to strongly typed values based on the parameter type.
/// </summary>
public static class SystemParameterTypeConverter
{
    /// <summary>
    /// Converts a string value to the specified type.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="value">The string value to convert.</param>
    /// <param name="type">The parameter type (string, int, decimal, bool, json).</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="ArgumentException">Thrown when conversion fails.</exception>
    public static T ConvertTo<T>(string value, string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        var normalizedType = type.ToLowerInvariant();

        try
        {
            return normalizedType switch
            {
                "string" => ConvertToString<T>(value),
                "int" => ConvertToInt<T>(value),
                "decimal" => ConvertToDecimal<T>(value),
                "bool" => ConvertToBool<T>(value),
                "json" => ConvertToJson<T>(value),
                _ => throw new ArgumentException($"Unsupported parameter type: {type}", nameof(type))
            };
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new ArgumentException($"Failed to convert value '{value}' to type '{type}': {ex.Message}", nameof(value), ex);
        }
    }

    /// <summary>
    /// Validates that a value can be converted to the specified type.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="type">The parameter type.</param>
    /// <returns>True if the value can be converted; otherwise, false.</returns>
    public static bool CanConvert(string value, string type)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(type))
            return false;

        var normalizedType = type.ToLowerInvariant();

        return normalizedType switch
        {
            "string" => true,
            "int" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "decimal" => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            "bool" => bool.TryParse(value, out _),
            "json" => TryParseJson(value),
            _ => false
        };
    }

    private static T ConvertToString<T>(string value)
    {
        if (typeof(T) != typeof(string))
            throw new ArgumentException($"Type mismatch: expected string but got {typeof(T).Name}");

        return (T)(object)value;
    }

    private static T ConvertToInt<T>(string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            throw new ArgumentException($"Cannot convert '{value}' to int", nameof(value));

        if (typeof(T) == typeof(int))
            return (T)(object)result;

        if (typeof(T) == typeof(long))
            return (T)(object)(long)result;

        if (typeof(T) == typeof(object))
            return (T)(object)result;

        throw new ArgumentException($"Type mismatch: cannot convert int to {typeof(T).Name}");
    }

    private static T ConvertToDecimal<T>(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
            throw new ArgumentException($"Cannot convert '{value}' to decimal", nameof(value));

        if (typeof(T) == typeof(decimal))
            return (T)(object)result;

        if (typeof(T) == typeof(double))
            return (T)(object)(double)result;

        if (typeof(T) == typeof(float))
            return (T)(object)(float)result;

        if (typeof(T) == typeof(object))
            return (T)(object)result;

        throw new ArgumentException($"Type mismatch: cannot convert decimal to {typeof(T).Name}");
    }

    private static T ConvertToBool<T>(string value)
    {
        if (!bool.TryParse(value, out var result))
            throw new ArgumentException($"Cannot convert '{value}' to bool", nameof(value));

        if (typeof(T) == typeof(bool) || typeof(T) == typeof(object))
            return (T)(object)result;

        throw new ArgumentException($"Type mismatch: cannot convert bool to {typeof(T).Name}");
    }

    private static T ConvertToJson<T>(string value)
    {
        try
        {
            var result = JsonSerializer.Deserialize<T>(value);
            if (result == null)
                throw new ArgumentException("JSON deserialization returned null", nameof(value));

            return result;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON: {ex.Message}", nameof(value), ex);
        }
    }

    private static bool TryParseJson(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
