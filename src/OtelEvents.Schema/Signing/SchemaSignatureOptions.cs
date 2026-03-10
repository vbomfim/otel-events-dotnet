namespace OtelEvents.Schema.Signing;

/// <summary>
/// Configuration for schema file signing and verification.
/// Supports multiple key sources: file path, environment variable, or Azure Key Vault reference.
/// </summary>
public sealed class SchemaSignatureOptions
{
    /// <summary>How the HMAC key is sourced.</summary>
    public SchemaSignatureKeySource KeySource { get; set; } = SchemaSignatureKeySource.EnvironmentVariable;

    /// <summary>
    /// The key reference value. Interpretation depends on <see cref="KeySource"/>:
    /// <list type="bullet">
    ///   <item><see cref="SchemaSignatureKeySource.File"/> — absolute or relative file path</item>
    ///   <item><see cref="SchemaSignatureKeySource.EnvironmentVariable"/> — environment variable name</item>
    ///   <item><see cref="SchemaSignatureKeySource.AzureKeyVault"/> — Key Vault secret URI</item>
    /// </list>
    /// </summary>
    public string KeyReference { get; set; } = "OTEL_SCHEMA_SIGNING_KEY";

    /// <summary>
    /// Resolves the raw HMAC key bytes from the configured key source.
    /// </summary>
    /// <returns>The key bytes for HMAC-SHA256 computation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the key cannot be resolved.</exception>
    public byte[] ResolveKey()
    {
        return KeySource switch
        {
            SchemaSignatureKeySource.File => ResolveFromFile(),
            SchemaSignatureKeySource.EnvironmentVariable => ResolveFromEnvironmentVariable(),
            SchemaSignatureKeySource.AzureKeyVault => throw new NotSupportedException(
                $"Azure Key Vault key source is planned for a future release. " +
                $"Use '{SchemaSignatureKeySource.File}' or '{SchemaSignatureKeySource.EnvironmentVariable}' instead."),
            _ => throw new InvalidOperationException($"Unknown key source: {KeySource}"),
        };
    }

    private byte[] ResolveFromFile()
    {
        if (string.IsNullOrWhiteSpace(KeyReference))
        {
            throw new InvalidOperationException(
                "KeyReference must specify a file path when KeySource is File.");
        }

        if (!System.IO.File.Exists(KeyReference))
        {
            throw new InvalidOperationException(
                $"Signing key file not found: '{KeyReference}'.");
        }

        var keyBytes = System.IO.File.ReadAllBytes(KeyReference);
        if (keyBytes.Length == 0)
        {
            throw new InvalidOperationException(
                $"Signing key file is empty: '{KeyReference}'.");
        }

        return keyBytes;
    }

    private byte[] ResolveFromEnvironmentVariable()
    {
        if (string.IsNullOrWhiteSpace(KeyReference))
        {
            throw new InvalidOperationException(
                "KeyReference must specify an environment variable name when KeySource is EnvironmentVariable.");
        }

        var value = Environment.GetEnvironmentVariable(KeyReference);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"Environment variable '{KeyReference}' is not set or is empty.");
        }

        return Convert.FromBase64String(value);
    }
}
