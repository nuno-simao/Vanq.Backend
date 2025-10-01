namespace Vanq.Shared;

/// <summary>
/// Provides utility methods for GUID validation.
/// </summary>
public static class GuidValidationUtils
{
    /// <summary>
    /// Ensures that the specified GUID is not empty.
    /// </summary>
    /// <param name="value">The GUID value to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <exception cref="ArgumentException">Thrown when the GUID is empty.</exception>
    public static void EnsureNotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{parameterName} identifier is required.", parameterName);
        }
    }

    /// <summary>
    /// Ensures that the executor identifier is not empty.
    /// </summary>
    /// <param name="executorId">The executor ID to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the executor ID is empty.</exception>
    public static void EnsureExecutor(Guid executorId)
    {
        EnsureNotEmpty(executorId, "Executor");
    }

    /// <summary>
    /// Ensures that multiple GUID identifiers are not empty.
    /// </summary>
    /// <param name="identifiers">A dictionary of parameter names and their corresponding GUID values.</param>
    /// <exception cref="ArgumentException">Thrown when any GUID is empty.</exception>
    public static void EnsureAllNotEmpty(params (string Name, Guid Value)[] identifiers)
    {
        foreach (var (name, value) in identifiers)
        {
            EnsureNotEmpty(value, name);
        }
    }
}
