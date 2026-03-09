namespace OtelEvents.Schema.Signing;

/// <summary>
/// The result of a schema signature verification operation.
/// </summary>
public sealed class SchemaVerificationResult
{
    /// <summary>Whether the signature is valid.</summary>
    public bool IsValid { get; }

    /// <summary>Error message when verification fails. Null on success.</summary>
    public string? Error { get; }

    private SchemaVerificationResult(bool isValid, string? error)
    {
        IsValid = isValid;
        Error = error;
    }

    /// <summary>Creates a successful verification result.</summary>
    public static SchemaVerificationResult Success() => new(true, null);

    /// <summary>Creates a failed verification result with an error message.</summary>
    public static SchemaVerificationResult Failure(string error) => new(false, error);
}
