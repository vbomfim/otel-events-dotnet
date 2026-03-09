namespace OtelEvents.Exporter.Json;

/// <summary>Configuration for the otel-events JSON exporter.</summary>
public sealed class OtelEventsJsonExporterOptions
{
    /// <summary>Output target: Stdout, Stderr, or File.</summary>
    public OtelEventsJsonOutput Output { get; set; } = OtelEventsJsonOutput.Stdout;

    /// <summary>
    /// File path for output when <see cref="Output"/> is <see cref="OtelEventsJsonOutput.File"/>.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>Schema version stamped into every envelope as "all.v".</summary>
    public string SchemaVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Environment profile that adjusts multiple security-sensitive defaults at once.
    /// Default: Production (most restrictive).
    /// </summary>
    public OtelEventsEnvironmentProfile EnvironmentProfile { get; set; } = OtelEventsEnvironmentProfile.Production;

    /// <summary>
    /// Controls exception detail in the JSON envelope.
    /// Default depends on EnvironmentProfile:
    ///   Development → Full, Staging → TypeAndMessage, Production → TypeAndMessage.
    /// </summary>
    public ExceptionDetailLevel? ExceptionDetailLevel { get; set; }

    /// <summary>
    /// Emit "all.host" and "all.pid" in the envelope.
    /// Default: false. These fields may expose internal infrastructure details.
    /// </summary>
    public bool EmitHostInfo { get; set; }

    /// <summary>
    /// Maximum length for any single attribute value (string fields).
    /// Default: 4096 characters.
    /// </summary>
    public int MaxAttributeValueLength { get; set; } = 4096;

    /// <summary>
    /// Allowlist of attribute names to emit for non-otel-events LogRecords.
    /// When set, only listed attributes pass through. Null = all attributes (default).
    /// </summary>
    public ISet<string>? AttributeAllowlist { get; set; }

    /// <summary>
    /// Denylist of attribute names to never emit. Takes precedence over allowlist.
    /// </summary>
    public ISet<string> AttributeDenylist { get; set; } = new HashSet<string>();

    /// <summary>
    /// Regex patterns for value-level redaction. Matching values are replaced with "[REDACTED]".
    /// </summary>
    public IList<string> RedactPatterns { get; set; } = [];

    /// <summary>
    /// Lock timeout for stream writes. Default: 100ms.
    /// </summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Resolves the effective <see cref="ExceptionDetailLevel"/> based on the
    /// explicit setting or the <see cref="EnvironmentProfile"/> default.
    /// </summary>
    internal ExceptionDetailLevel ResolvedExceptionDetailLevel =>
        ExceptionDetailLevel ?? EnvironmentProfile switch
        {
            OtelEventsEnvironmentProfile.Development => Json.ExceptionDetailLevel.Full,
            OtelEventsEnvironmentProfile.Staging => Json.ExceptionDetailLevel.TypeAndMessage,
            OtelEventsEnvironmentProfile.Production => Json.ExceptionDetailLevel.TypeAndMessage,
            _ => Json.ExceptionDetailLevel.TypeAndMessage,
        };
}
