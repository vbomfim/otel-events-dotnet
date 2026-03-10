namespace OtelEvents.Exporter.Json;

/// <summary>
/// Data sensitivity classification for attribute values.
/// Controls redaction behavior per <see cref="OtelEventsEnvironmentProfile"/>.
/// </summary>
/// <remarks>
/// Mirrors <c>OtelEvents.Schema.Models.Sensitivity</c> for use in the exporter
/// without requiring a dependency on the Schema package.
/// Spec reference: SPECIFICATION.md §16.2.
/// </remarks>
public enum OtelEventsSensitivity
{
    /// <summary>Safe to emit in all environments.</summary>
    Public,

    /// <summary>Internal infrastructure details. Redacted in Production.</summary>
    Internal,

    /// <summary>Personally Identifiable Information. Redacted in Production and Staging.</summary>
    Pii,

    /// <summary>Secrets, tokens, API keys. Always redacted (even in Development).</summary>
    Credential,
}
