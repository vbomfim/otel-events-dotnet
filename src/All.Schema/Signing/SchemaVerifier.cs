using System.Security.Cryptography;

namespace All.Schema.Signing;

/// <summary>
/// Validates HMAC-SHA256 signatures for .all.yaml schema files.
/// Used to verify schema integrity before code generation in multi-team environments.
/// </summary>
public sealed class SchemaVerifier
{
    /// <summary>
    /// Verifies that a schema file's content matches its detached signature.
    /// </summary>
    /// <param name="schemaContent">The raw schema file content bytes.</param>
    /// <param name="expectedSignature">The hex-encoded expected HMAC-SHA256 signature.</param>
    /// <param name="key">The HMAC key bytes.</param>
    /// <returns>Verification result indicating success or failure with reason.</returns>
    public static SchemaVerificationResult Verify(byte[] schemaContent, string expectedSignature, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(schemaContent);
        ArgumentNullException.ThrowIfNull(key);

        if (string.IsNullOrWhiteSpace(expectedSignature))
        {
            return SchemaVerificationResult.Failure("Signature is missing or empty.");
        }

        if (key.Length == 0)
        {
            return SchemaVerificationResult.Failure("Verification key must not be empty.");
        }

        var actualSignature = SchemaSigner.ComputeSignature(schemaContent, key);

        // Use constant-time comparison to prevent timing attacks
        var isValid = CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(actualSignature),
            Convert.FromHexString(expectedSignature));

        return isValid
            ? SchemaVerificationResult.Success()
            : SchemaVerificationResult.Failure("Signature mismatch — schema content has been tampered with or the wrong key was used.");
    }

    /// <summary>
    /// Verifies a schema file against its detached .sig file.
    /// </summary>
    /// <param name="schemaFilePath">Path to the .all.yaml schema file.</param>
    /// <param name="key">The HMAC key bytes.</param>
    /// <returns>Verification result.</returns>
    public static SchemaVerificationResult VerifyFile(string schemaFilePath, byte[] key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaFilePath);

        if (!File.Exists(schemaFilePath))
        {
            return SchemaVerificationResult.Failure($"Schema file not found: '{schemaFilePath}'.");
        }

        var signatureFilePath = schemaFilePath + SchemaSigner.SignatureFileExtension;
        if (!File.Exists(signatureFilePath))
        {
            return SchemaVerificationResult.Failure(
                $"Signature file not found: '{signatureFilePath}'. Run 'dotnet all sign' to generate it.");
        }

        var content = File.ReadAllBytes(schemaFilePath);
        var expectedSignature = File.ReadAllText(signatureFilePath).Trim();

        return Verify(content, expectedSignature, key);
    }
}
