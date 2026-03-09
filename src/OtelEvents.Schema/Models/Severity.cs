namespace OtelEvents.Schema.Models;

/// <summary>
/// Event severity levels matching OpenTelemetry log severity.
/// </summary>
public enum Severity
{
    Trace,
    Debug,
    Info,
    Warn,
    Error,
    Fatal
}

/// <summary>
/// Extension methods for Severity parsing and validation.
/// </summary>
public static class SeverityExtensions
{
    private static readonly Dictionary<string, Severity> SeverityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TRACE"] = Severity.Trace,
        ["DEBUG"] = Severity.Debug,
        ["INFO"] = Severity.Info,
        ["WARN"] = Severity.Warn,
        ["ERROR"] = Severity.Error,
        ["FATAL"] = Severity.Fatal
    };

    /// <summary>
    /// Tries to parse a YAML severity string into a <see cref="Severity"/>.
    /// </summary>
    public static bool TryParseSeverity(string value, out Severity severity)
    {
        return SeverityMap.TryGetValue(value, out severity);
    }

    /// <summary>
    /// Returns the set of valid severity level names.
    /// </summary>
    public static IReadOnlyCollection<string> ValidSeverityNames => SeverityMap.Keys;
}
