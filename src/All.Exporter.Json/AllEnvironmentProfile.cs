namespace All.Exporter.Json;

/// <summary>Environment profiles adjust multiple security defaults simultaneously.</summary>
public enum AllEnvironmentProfile
{
    /// <summary>Most permissive: full exception details, all sensitivity levels visible.</summary>
    Development,

    /// <summary>Moderate: TypeAndMessage exceptions, PII fields redacted.</summary>
    Staging,

    /// <summary>Most restrictive (default): TypeAndMessage exceptions, PII and internal redacted.</summary>
    Production,
}
