namespace Vanq.CLI.Output;

/// <summary>
/// Defines the contract for output formatters that display data in different formats
/// </summary>
public interface IOutputFormatter
{
    /// <summary>
    /// Displays the provided data in the formatter's specific format
    /// </summary>
    /// <typeparam name="T">The type of data to display</typeparam>
    /// <param name="data">The data to display</param>
    /// <param name="title">Optional title to display above the data</param>
    void Display<T>(T data, string? title = null);
}
