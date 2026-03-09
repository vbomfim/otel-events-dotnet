namespace All.Schema.Validation;

/// <summary>
/// A structured validation error with an error code (e.g., ALL_SCHEMA_001).
/// Returned by <see cref="SchemaValidator"/> during build-time validation.
/// </summary>
public sealed class SchemaError
{
    /// <summary>Error code (e.g., "ALL_SCHEMA_001").</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable error description.</summary>
    public required string Message { get; init; }

    public override string ToString() => $"{Code}: {Message}";
}
