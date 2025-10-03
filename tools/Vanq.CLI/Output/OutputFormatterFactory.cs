namespace Vanq.CLI.Output;

/// <summary>
/// Factory for creating output formatters based on format string
/// </summary>
public static class OutputFormatterFactory
{
    /// <summary>
    /// Creates an output formatter for the specified format
    /// </summary>
    /// <param name="format">The format type: "json", "table", or "csv"</param>
    /// <returns>An instance of the appropriate formatter</returns>
    /// <exception cref="ArgumentException">Thrown when format is not recognized</exception>
    public static IOutputFormatter Create(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "json" => new JsonOutputFormatter(),
            "table" => new TableOutputFormatter(),
            "csv" => new CsvOutputFormatter(),
            _ => throw new ArgumentException(
                $"Unknown output format '{format}'. Valid formats are: json, table, csv",
                nameof(format))
        };
    }

    /// <summary>
    /// Attempts to create an output formatter, returning null if format is invalid
    /// </summary>
    /// <param name="format">The format type: "json", "table", or "csv"</param>
    /// <returns>An instance of the appropriate formatter, or null if format is invalid</returns>
    public static IOutputFormatter? TryCreate(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return null;
        }

        try
        {
            return Create(format);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the default output formatter (Table)
    /// </summary>
    public static IOutputFormatter Default => new TableOutputFormatter();

    /// <summary>
    /// Gets all supported format names
    /// </summary>
    public static string[] SupportedFormats => new[] { "json", "table", "csv" };
}
