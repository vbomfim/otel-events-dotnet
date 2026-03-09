namespace OtelEvents.Schema.Signing;

/// <summary>
/// The result of a schema signing operation.
/// </summary>
public sealed class SchemaSignatureResult
{
    /// <summary>The hex-encoded HMAC-SHA256 signature.</summary>
    public required string Signature { get; init; }

    /// <summary>The path to the generated .sig file, if written to disk.</summary>
    public string? SignatureFilePath { get; init; }
}
