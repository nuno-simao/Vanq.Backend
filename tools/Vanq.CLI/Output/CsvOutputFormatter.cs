using System.Collections;
using System.Reflection;
using System.Text;

namespace Vanq.CLI.Output;

/// <summary>
/// Outputs data as CSV format with headers
/// </summary>
public class CsvOutputFormatter : IOutputFormatter
{
    public void Display<T>(T data, string? title = null)
    {
        if (data is null)
        {
            Console.WriteLine("# No data");
            return;
        }

        if (title is not null)
        {
            Console.WriteLine($"# {title}");
        }

        // Handle collections
        if (data is IEnumerable enumerable and not string)
        {
            DisplayCollection(enumerable);
        }
        else
        {
            DisplaySingleObject(data);
        }
    }

    private static void DisplayCollection(IEnumerable collection)
    {
        var items = collection.Cast<object>().ToList();

        if (items.Count == 0)
        {
            Console.WriteLine("# Empty collection");
            return;
        }

        var firstItem = items[0];
        var properties = GetDisplayProperties(firstItem.GetType());

        if (properties.Length == 0)
        {
            // Fallback for simple types
            Console.WriteLine("Value");
            foreach (var item in items)
            {
                Console.WriteLine(EscapeCsvValue(item?.ToString() ?? ""));
            }
            return;
        }

        // Write header
        Console.WriteLine(string.Join(",", properties.Select(p => p.Name)));

        // Write rows
        foreach (var item in items)
        {
            var values = properties
                .Select(p => FormatCsvValue(p.GetValue(item)))
                .Select(EscapeCsvValue);
            Console.WriteLine(string.Join(",", values));
        }
    }

    private static void DisplaySingleObject(object obj)
    {
        var properties = GetDisplayProperties(obj.GetType());

        if (properties.Length == 0)
        {
            // Simple value
            Console.WriteLine("Value");
            Console.WriteLine(EscapeCsvValue(obj.ToString() ?? ""));
            return;
        }

        // Write headers
        Console.WriteLine(string.Join(",", properties.Select(p => p.Name)));

        // Write values
        var values = properties
            .Select(p => FormatCsvValue(p.GetValue(obj)))
            .Select(EscapeCsvValue);
        Console.WriteLine(string.Join(",", values));
    }

    private static PropertyInfo[] GetDisplayProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .OrderBy(p => p.Name)
            .ToArray();
    }

    private static string FormatCsvValue(object? value)
    {
        if (value is null)
        {
            return "";
        }

        if (value is bool boolValue)
        {
            return boolValue.ToString().ToLowerInvariant();
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        if (value is IEnumerable enumerable and not string)
        {
            var items = enumerable.Cast<object>().ToList();
            return string.Join(";", items.Select(i => i.ToString()));
        }

        return value.ToString() ?? "";
    }

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // If value contains comma, quote, or newline, wrap in quotes and escape quotes
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
