using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vanq.CLI.Output;

/// <summary>
/// Outputs data as formatted JSON
/// </summary>
public class JsonOutputFormatter : IOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public void Display<T>(T data, string? title = null)
    {
        if (title is not null)
        {
            Console.WriteLine($"# {title}");
            Console.WriteLine();
        }

        var json = JsonSerializer.Serialize(data, JsonOptions);
        Console.WriteLine(json);
    }
}
