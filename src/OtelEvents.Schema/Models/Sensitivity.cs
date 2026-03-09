namespace OtelEvents.Schema.Models;

/// <summary>
/// Data sensitivity classification for field values.
/// Controls redaction behavior in different environments.
/// </summary>
public enum Sensitivity
{
    /// <summary>Safe to emit in all environments. Default if not specified.</summary>
    Public,

    /// <summary>Internal infrastructure details. Redacted in Production.</summary>
    Internal,

    /// <summary>Personally Identifiable Information. Redacted in Production/Staging.</summary>
    Pii,

    /// <summary>Secrets, tokens, API keys. Always redacted.</summary>
    Credential
}

/// <summary>
/// Extension methods for Sensitivity parsing and validation.
/// </summary>
public static class SensitivityExtensions
{
    private static readonly Dictionary<string, Sensitivity> SensitivityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["public"] = Sensitivity.Public,
        ["internal"] = Sensitivity.Internal,
        ["pii"] = Sensitivity.Pii,
        ["credential"] = Sensitivity.Credential
    };

    /// <summary>
    /// Tries to parse a YAML sensitivity string into a <see cref="Sensitivity"/>.
    /// </summary>
    public static bool TryParseSensitivity(string value, out Sensitivity sensitivity)
    {
        return SensitivityMap.TryGetValue(value, out sensitivity);
    }

    /// <summary>
    /// Returns the set of valid sensitivity level names.
    /// </summary>
    public static IReadOnlyCollection<string> ValidSensitivityNames => SensitivityMap.Keys;
}
