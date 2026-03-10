using System.Security.Cryptography;

namespace OtelEvents.Schema.Signing;

/// <summary>
/// Generates HMAC-SHA256 signatures for .otel.yaml schema files.
/// Used to ensure schema integrity in multi-team environments.
/// </summary>
public sealed class SchemaSigner
{
    /// <summary>File extension appended to schema files for detached signatures.</summary>
    public const string SignatureFileExtension = ".sig";

    /// <summary>
    /// Signs schema content and returns the hex-encoded HMAC-SHA256 signature.
    /// </summary>
    /// <param name="schemaContent">The raw schema file content bytes.</param>
    /// <param name="key">The HMAC key bytes.</param>
    /// <returns>Hex-encoded HMAC-SHA256 signature string.</returns>
    public static string ComputeSignature(byte[] schemaContent, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(schemaContent);
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length == 0)
        {
            throw new ArgumentException("Signing key must not be empty.", nameof(key));
        }

        var hash = HMACSHA256.HashData(key, schemaContent);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Signs a schema file and writes the signature to a detached .sig file.
    /// </summary>
    /// <param name="schemaFilePath">Path to the .otel.yaml schema file.</param>
    /// <param name="key">The HMAC key bytes.</param>
    /// <returns>The signing result containing the signature and .sig file path.</returns>
    public static SchemaSignatureResult SignFile(string schemaFilePath, byte[] key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaFilePath);

        if (!File.Exists(schemaFilePath))
        {
            throw new FileNotFoundException(
                $"Schema file not found: '{schemaFilePath}'.", schemaFilePath);
        }

        var content = File.ReadAllBytes(schemaFilePath);
        var signature = ComputeSignature(content, key);
        var signatureFilePath = schemaFilePath + SignatureFileExtension;

        File.WriteAllText(signatureFilePath, signature);

        return new SchemaSignatureResult
        {
            Signature = signature,
            SignatureFilePath = signatureFilePath,
        };
    }
}
