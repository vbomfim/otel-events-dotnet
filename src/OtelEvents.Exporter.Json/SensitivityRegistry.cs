namespace OtelEvents.Exporter.Json;

/// <summary>
/// Registry mapping attribute names to <see cref="OtelEventsSensitivity"/> classifications.
/// Determines which fields are redacted based on the active <see cref="OtelEventsEnvironmentProfile"/>.
/// </summary>
/// <remarks>
/// Populated from:
/// <list type="bullet">
///   <item>Built-in mappings for known integration pack fields</item>
///   <item>Schema metadata (when available)</item>
///   <item>Manual registrations via <see cref="OtelEventsJsonExporterOptions.SensitivityMappings"/></item>
/// </list>
/// Spec reference: SPECIFICATION.md §16.2 (PII Classification Framework).
/// </remarks>
internal sealed class SensitivityRegistry
{
    private readonly Dictionary<string, OtelEventsSensitivity> _mappings;

    /// <summary>
    /// Built-in sensitivity mappings for known integration pack fields.
    /// </summary>
    private static readonly Dictionary<string, OtelEventsSensitivity> BuiltInMappings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // PII fields (redacted in Production and Staging)
            ["clientIp"] = OtelEventsSensitivity.Pii,
            ["userAgent"] = OtelEventsSensitivity.Pii,
            ["identityHint"] = OtelEventsSensitivity.Pii,

            // Internal fields (redacted in Production only)
            ["errorMessage"] = OtelEventsSensitivity.Internal,
            ["endpoint"] = OtelEventsSensitivity.Internal,
            ["hostName"] = OtelEventsSensitivity.Internal,
            ["grpcStatusDetail"] = OtelEventsSensitivity.Internal,
            ["cosmosQueryText"] = OtelEventsSensitivity.Internal,

            // Credential fields (always redacted, even in Development)
            ["apiKey"] = OtelEventsSensitivity.Credential,
            ["apiSecret"] = OtelEventsSensitivity.Credential,
            ["accessToken"] = OtelEventsSensitivity.Credential,
            ["secretKey"] = OtelEventsSensitivity.Credential,
            ["connectionString"] = OtelEventsSensitivity.Credential,
            ["password"] = OtelEventsSensitivity.Credential,
            ["token"] = OtelEventsSensitivity.Credential,
        };

    public SensitivityRegistry()
    {
        _mappings = new Dictionary<string, OtelEventsSensitivity>(
            BuiltInMappings, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Registers a field with a specific sensitivity level.
    /// Overwrites any existing mapping for the same field name.
    /// </summary>
    public void Register(string fieldName, OtelEventsSensitivity sensitivity)
    {
        _mappings[fieldName] = sensitivity;
    }

    /// <summary>
    /// Tries to get the sensitivity classification for a field.
    /// </summary>
    /// <returns><c>true</c> if the field has a known sensitivity; otherwise <c>false</c>.</returns>
    public bool TryGetSensitivity(string fieldName, out OtelEventsSensitivity sensitivity)
    {
        return _mappings.TryGetValue(fieldName, out sensitivity);
    }

    /// <summary>
    /// Determines whether a value with the given sensitivity should be redacted
    /// in the specified environment profile.
    /// </summary>
    /// <remarks>
    /// Matrix (from SPECIFICATION.md §16.2):
    /// <code>
    /// Profile      | Public | Internal | Pii    | Credential
    /// -------------|--------|----------|--------|----------
    /// Development  | ✓      | ✓        | ✓      | REDACTED
    /// Staging      | ✓      | ✓        | REDACT | REDACTED
    /// Production   | ✓      | REDACT   | REDACT | REDACTED
    /// </code>
    /// </remarks>
    public static bool ShouldRedact(OtelEventsSensitivity sensitivity, OtelEventsEnvironmentProfile profile)
    {
        return (profile, sensitivity) switch
        {
            (_, OtelEventsSensitivity.Public) => false,
            (_, OtelEventsSensitivity.Credential) => true,
            (OtelEventsEnvironmentProfile.Development, _) => false,
            (OtelEventsEnvironmentProfile.Staging, OtelEventsSensitivity.Pii) => true,
            (OtelEventsEnvironmentProfile.Production, OtelEventsSensitivity.Internal) => true,
            (OtelEventsEnvironmentProfile.Production, OtelEventsSensitivity.Pii) => true,
            _ => false,
        };
    }

    /// <summary>
    /// Returns the redaction placeholder for a given sensitivity level.
    /// Format: <c>[REDACTED:{level}]</c> (e.g., <c>[REDACTED:pii]</c>).
    /// </summary>
    public static string GetRedactedValue(OtelEventsSensitivity sensitivity)
    {
        return sensitivity switch
        {
            OtelEventsSensitivity.Internal => "[REDACTED:internal]",
            OtelEventsSensitivity.Pii => "[REDACTED:pii]",
            OtelEventsSensitivity.Credential => "[REDACTED:credential]",
            _ => "[REDACTED]",
        };
    }
}
