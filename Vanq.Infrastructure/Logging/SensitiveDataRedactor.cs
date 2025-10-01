using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Vanq.Infrastructure.Logging;

public sealed class SensitiveDataRedactor
{
    private readonly LoggingOptions _options;
    private readonly HashSet<string> _maskedFields;
    private readonly Regex _emailRegex = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled);
    private readonly Regex _cpfRegex = new(@"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b", RegexOptions.Compiled);
    private readonly Regex _phoneRegex = new(@"\b\(?\d{2}\)?\s?\d{4,5}-?\d{4}\b", RegexOptions.Compiled);

    public SensitiveDataRedactor(IOptions<LoggingOptions> options)
    {
        _options = options.Value;
        _maskedFields = new HashSet<string>(
            _options.MaskedFields,
            StringComparer.OrdinalIgnoreCase
        );
    }

    public string RedactJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var redacted = RedactElement(document.RootElement);
            return JsonSerializer.Serialize(redacted);
        }
        catch
        {
            return RedactPlainText(json);
        }
    }

    public string RedactPlainText(string text)
    {
        var redacted = text;
        redacted = _emailRegex.Replace(redacted, _options.SensitiveValuePlaceholder);
        redacted = _cpfRegex.Replace(redacted, _options.SensitiveValuePlaceholder);
        redacted = _phoneRegex.Replace(redacted, _options.SensitiveValuePlaceholder);
        return redacted;
    }

    private object? RedactElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => RedactObject(element),
            JsonValueKind.Array => RedactArray(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private Dictionary<string, object?> RedactObject(JsonElement obj)
    {
        var result = new Dictionary<string, object?>();

        foreach (var property in obj.EnumerateObject())
        {
            if (_maskedFields.Contains(property.Name))
            {
                result[property.Name] = _options.SensitiveValuePlaceholder;
            }
            else
            {
                result[property.Name] = RedactElement(property.Value);
            }
        }

        return result;
    }

    private List<object?> RedactArray(JsonElement array)
    {
        var result = new List<object?>();

        foreach (var item in array.EnumerateArray())
        {
            result.Add(RedactElement(item));
        }

        return result;
    }

    public bool ShouldRedactField(string fieldName)
    {
        return _maskedFields.Contains(fieldName);
    }
}
