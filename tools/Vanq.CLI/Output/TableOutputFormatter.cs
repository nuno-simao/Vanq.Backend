using Spectre.Console;
using System.Collections;
using System.Reflection;

namespace Vanq.CLI.Output;

/// <summary>
/// Outputs data as formatted table using Spectre.Console
/// </summary>
public class TableOutputFormatter : IOutputFormatter
{
    public void Display<T>(T data, string? title = null)
    {
        if (data is null)
        {
            AnsiConsole.MarkupLine("[yellow]No data to display[/]");
            return;
        }

        if (title is not null)
        {
            AnsiConsole.MarkupLine($"[bold blue]{title}[/]");
            AnsiConsole.WriteLine();
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
            AnsiConsole.MarkupLine("[yellow]Empty collection[/]");
            return;
        }

        var firstItem = items[0];
        var properties = GetDisplayProperties(firstItem.GetType());

        if (properties.Length == 0)
        {
            // Fallback for simple types
            var table = new Table();
            table.AddColumn("Value");

            foreach (var item in items)
            {
                table.AddRow(item?.ToString() ?? "[grey]null[/]");
            }

            AnsiConsole.Write(table);
            return;
        }

        // Create table with property columns
        var dataTable = new Table();
        foreach (var prop in properties)
        {
            dataTable.AddColumn(new TableColumn(prop.Name).Centered());
        }

        // Add rows
        foreach (var item in items)
        {
            var values = properties
                .Select(p => FormatValue(p.GetValue(item)))
                .ToArray();
            dataTable.AddRow(values);
        }

        AnsiConsole.Write(dataTable);
    }

    private static void DisplaySingleObject(object obj)
    {
        var properties = GetDisplayProperties(obj.GetType());

        if (properties.Length == 0)
        {
            // Simple value
            AnsiConsole.WriteLine(obj.ToString() ?? "[grey]null[/]");
            return;
        }

        var table = new Table();
        table.AddColumn("Property");
        table.AddColumn("Value");

        foreach (var prop in properties)
        {
            var value = FormatValue(prop.GetValue(obj));
            table.AddRow(prop.Name, value);
        }

        AnsiConsole.Write(table);
    }

    private static PropertyInfo[] GetDisplayProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .OrderBy(p => p.Name)
            .ToArray();
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "[grey]null[/]";
        }

        if (value is bool boolValue)
        {
            return boolValue ? "[green]true[/]" : "[red]false[/]";
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        if (value is IEnumerable enumerable and not string)
        {
            var items = enumerable.Cast<object>().Take(3).ToList();
            var display = string.Join(", ", items.Select(i => i.ToString()));
            return items.Count > 3 ? $"{display}..." : display;
        }

        return value.ToString() ?? "[grey]null[/]";
    }
}
