using System.CommandLine.Invocation;
using System.CommandLine;

namespace Vanq.CLI.Commands;

/// <summary>
/// Helper class to extract global options from InvocationContext.
/// </summary>
public static class GlobalOptionsHelper
{
    public static bool GetVerbose(InvocationContext context) =>
        GetGlobalOption<bool>(context, "--verbose");

    public static string GetOutputFormat(InvocationContext context) =>
        GetGlobalOption<string>(context, "--output") ?? "table";

    public static string? GetProfile(InvocationContext context) =>
        GetGlobalOption<string?>(context, "--profile");

    public static bool GetNoColor(InvocationContext context) =>
        GetGlobalOption<bool>(context, "--no-color");

    public static bool GetForce(InvocationContext context) =>
        GetGlobalOption<bool>(context, "--force");

    private static T? GetGlobalOption<T>(InvocationContext context, string optionName)
    {
        var option = context.ParseResult.RootCommandResult.Command.Options
            .FirstOrDefault(o => o.HasAlias(optionName));

        if (option is Option<T> typedOption)
        {
            return context.ParseResult.GetValueForOption(typedOption);
        }

        return default;
    }
}
