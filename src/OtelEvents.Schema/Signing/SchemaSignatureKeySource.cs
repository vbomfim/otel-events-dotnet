namespace OtelEvents.Schema.Signing;

/// <summary>
/// Defines how the HMAC key is sourced for schema signing/verification.
/// </summary>
public enum SchemaSignatureKeySource
{
    /// <summary>Key is loaded from a file at the specified path.</summary>
    File,

    /// <summary>Key is loaded from an environment variable.</summary>
    EnvironmentVariable,

    /// <summary>Key is referenced from Azure Key Vault (URI format).</summary>
    AzureKeyVault,
}
