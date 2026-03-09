using System.Security.Cryptography;
using System.Text;
using OtelEvents.Schema.Signing;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for schema signing and verification (Feature 3.10).
/// Covers: SchemaSigner, SchemaVerifier, SchemaSignatureOptions, SchemaVerificationResult.
/// Uses known test vectors and validates tampered-content detection.
/// </summary>
public sealed class SchemaSigningTests : IDisposable
{
    private readonly string _tempDir;

    // Known test key (32 bytes, base64-encoded for env var tests)
    private static readonly byte[] TestKey = Encoding.UTF8.GetBytes("test-signing-key-for-all-schema!");
    private static readonly string TestKeyBase64 = Convert.ToBase64String(TestKey);

    private const string SampleSchema = """
        schema:
          name: "OrderService"
          version: "1.0.0"
          namespace: "Acme.Orders"
        events:
          order.placed:
            id: 1001
            severity: INFO
            message: "Order {orderId} placed"
            fields:
              orderId:
                type: string
                required: true
        """;

    public SchemaSigningTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"all-schema-sign-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string WriteTempSchema(string content, string fileName = "orders.all.yaml")
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    // ── SchemaSigner.ComputeSignature — Known Test Vectors ──────────────

    [Fact]
    public void ComputeSignature_KnownInput_ProducesExpectedHmacSha256()
    {
        var content = Encoding.UTF8.GetBytes(SampleSchema);

        var signature = SchemaSigner.ComputeSignature(content, TestKey);

        // Verify it's a 64-char lowercase hex string (SHA256 = 32 bytes = 64 hex chars)
        Assert.Equal(64, signature.Length);
        Assert.Matches("^[0-9a-f]{64}$", signature);
    }

    [Fact]
    public void ComputeSignature_SameInput_ProducesDeterministicResult()
    {
        var content = Encoding.UTF8.GetBytes(SampleSchema);

        var sig1 = SchemaSigner.ComputeSignature(content, TestKey);
        var sig2 = SchemaSigner.ComputeSignature(content, TestKey);

        Assert.Equal(sig1, sig2);
    }

    [Fact]
    public void ComputeSignature_DifferentKeys_ProduceDifferentSignatures()
    {
        var content = Encoding.UTF8.GetBytes(SampleSchema);
        var otherKey = Encoding.UTF8.GetBytes("different-key-for-testing-12345!");

        var sig1 = SchemaSigner.ComputeSignature(content, TestKey);
        var sig2 = SchemaSigner.ComputeSignature(content, otherKey);

        Assert.NotEqual(sig1, sig2);
    }

    [Fact]
    public void ComputeSignature_DifferentContent_ProduceDifferentSignatures()
    {
        var content1 = Encoding.UTF8.GetBytes(SampleSchema);
        var content2 = Encoding.UTF8.GetBytes(SampleSchema + "\n# tampered");

        var sig1 = SchemaSigner.ComputeSignature(content1, TestKey);
        var sig2 = SchemaSigner.ComputeSignature(content2, TestKey);

        Assert.NotEqual(sig1, sig2);
    }

    [Fact]
    public void ComputeSignature_NullContent_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => SchemaSigner.ComputeSignature(null!, TestKey));
    }

    [Fact]
    public void ComputeSignature_NullKey_ThrowsArgumentNull()
    {
        var content = Encoding.UTF8.GetBytes(SampleSchema);
        Assert.Throws<ArgumentNullException>(() => SchemaSigner.ComputeSignature(content, null!));
    }

    [Fact]
    public void ComputeSignature_EmptyKey_ThrowsArgument()
    {
        var content = Encoding.UTF8.GetBytes(SampleSchema);
        Assert.Throws<ArgumentException>(() => SchemaSigner.ComputeSignature(content, []));
    }

    [Fact]
    public void ComputeSignature_MatchesSystemHmacSha256()
    {
        // Cross-check against .NET's own HMAC-SHA256 to ensure correctness
        var content = Encoding.UTF8.GetBytes(SampleSchema);
        var expected = Convert.ToHexStringLower(HMACSHA256.HashData(TestKey, content));

        var actual = SchemaSigner.ComputeSignature(content, TestKey);

        Assert.Equal(expected, actual);
    }

    // ── SchemaSigner.SignFile — File Operations ─────────────────────────

    [Fact]
    public void SignFile_CreatesDetachedSigFile()
    {
        var schemaPath = WriteTempSchema(SampleSchema);

        var result = SchemaSigner.SignFile(schemaPath, TestKey);

        Assert.NotNull(result.SignatureFilePath);
        Assert.True(File.Exists(result.SignatureFilePath));
        Assert.Equal(schemaPath + ".sig", result.SignatureFilePath);
    }

    [Fact]
    public void SignFile_SigFileContainsSignature()
    {
        var schemaPath = WriteTempSchema(SampleSchema);

        var result = SchemaSigner.SignFile(schemaPath, TestKey);

        var sigContent = File.ReadAllText(result.SignatureFilePath!).Trim();
        Assert.Equal(result.Signature, sigContent);
    }

    [Fact]
    public void SignFile_NonExistentFile_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(
            () => SchemaSigner.SignFile("/nonexistent/schema.all.yaml", TestKey));
    }

    [Fact]
    public void SignFile_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => SchemaSigner.SignFile(null!, TestKey));
    }

    [Fact]
    public void SignFile_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => SchemaSigner.SignFile("", TestKey));
    }

    // ── SchemaVerifier.Verify — In-Memory Verification ──────────────────

    [Fact]
    public void Verify_ValidSignature_ReturnsSuccess()
    {
        var content = Encoding.UTF8.GetBytes(SampleSchema);
        var signature = SchemaSigner.ComputeSignature(content, TestKey);

        var result = SchemaVerifier.Verify(content, signature, TestKey);

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Verify_TamperedContent_DetectsMismatch()
    {
        var originalContent = Encoding.UTF8.GetBytes(SampleSchema);
        var signature = SchemaSigner.ComputeSignature(originalContent, TestKey);
        var tamperedContent = Encoding.UTF8.GetBytes(SampleSchema.Replace("OrderService", "HackedService"));

        var result = SchemaVerifier.Verify(tamperedContent, signature, TestKey);

        Assert.False(result.IsValid);
        Assert.Contains("tampered", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_WrongKey_DetectsMismatch()
    {
        var content = Encoding.UTF8.GetBytes(SampleSchema);
        var signature = SchemaSigner.ComputeSignature(content, TestKey);
        var wrongKey = Encoding.UTF8.GetBytes("wrong-key-not-the-original-key!!");

        var result = SchemaVerifier.Verify(content, signature, wrongKey);

        Assert.False(result.IsValid);
        Assert.Contains("wrong key", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_EmptySignature_ReturnsFailure()
    {
        var content = Encoding.UTF8.GetBytes(SampleSchema);

        var result = SchemaVerifier.Verify(content, "", TestKey);

        Assert.False(result.IsValid);
        Assert.Contains("missing", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_NullSignature_ReturnsFailure()
    {
        var content = Encoding.UTF8.GetBytes(SampleSchema);

        var result = SchemaVerifier.Verify(content, null!, TestKey);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Verify_EmptyKey_ReturnsFailure()
    {
        var content = Encoding.UTF8.GetBytes(SampleSchema);
        var signature = "0000000000000000000000000000000000000000000000000000000000000000";

        var result = SchemaVerifier.Verify(content, signature, []);

        Assert.False(result.IsValid);
        Assert.Contains("empty", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    // ── SchemaVerifier.VerifyFile — File-Based Verification ─────────────

    [Fact]
    public void VerifyFile_ValidSignature_ReturnsSuccess()
    {
        var schemaPath = WriteTempSchema(SampleSchema);
        SchemaSigner.SignFile(schemaPath, TestKey);

        var result = SchemaVerifier.VerifyFile(schemaPath, TestKey);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void VerifyFile_TamperedSchema_DetectsMismatch()
    {
        var schemaPath = WriteTempSchema(SampleSchema);
        SchemaSigner.SignFile(schemaPath, TestKey);

        // Tamper with the schema after signing
        File.WriteAllText(schemaPath, SampleSchema + "\n# injected malicious content");

        var result = SchemaVerifier.VerifyFile(schemaPath, TestKey);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void VerifyFile_MissingSigFile_ReturnsFailure()
    {
        var schemaPath = WriteTempSchema(SampleSchema);
        // No .sig file created

        var result = SchemaVerifier.VerifyFile(schemaPath, TestKey);

        Assert.False(result.IsValid);
        Assert.Contains("Signature file not found", result.Error!);
    }

    [Fact]
    public void VerifyFile_NonExistentSchema_ReturnsFailure()
    {
        var result = SchemaVerifier.VerifyFile("/nonexistent/schema.all.yaml", TestKey);

        Assert.False(result.IsValid);
        Assert.Contains("not found", result.Error!);
    }

    [Fact]
    public void VerifyFile_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => SchemaVerifier.VerifyFile(null!, TestKey));
    }

    // ── Round-Trip: Sign → Verify ───────────────────────────────────────

    [Fact]
    public void SignThenVerify_RoundTrip_Succeeds()
    {
        var schemaPath = WriteTempSchema(SampleSchema);

        SchemaSigner.SignFile(schemaPath, TestKey);
        var result = SchemaVerifier.VerifyFile(schemaPath, TestKey);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SignThenVerify_DifferentSchemaFiles_IndependentSignatures()
    {
        var schema1 = WriteTempSchema(SampleSchema, "schema1.all.yaml");
        var schema2 = WriteTempSchema(SampleSchema + "\n# different", "schema2.all.yaml");

        SchemaSigner.SignFile(schema1, TestKey);
        SchemaSigner.SignFile(schema2, TestKey);

        // Each validates against its own signature
        Assert.True(SchemaVerifier.VerifyFile(schema1, TestKey).IsValid);
        Assert.True(SchemaVerifier.VerifyFile(schema2, TestKey).IsValid);

        // Cross-verify should fail: swap sig files
        var sig1 = File.ReadAllText(schema1 + ".sig");
        var sig2 = File.ReadAllText(schema2 + ".sig");
        File.WriteAllText(schema1 + ".sig", sig2);
        File.WriteAllText(schema2 + ".sig", sig1);

        Assert.False(SchemaVerifier.VerifyFile(schema1, TestKey).IsValid);
        Assert.False(SchemaVerifier.VerifyFile(schema2, TestKey).IsValid);
    }

    // ── SchemaSignatureOptions — Key Resolution ─────────────────────────

    [Fact]
    public void Options_ResolveKey_FromEnvironmentVariable()
    {
        var envVarName = $"ALL_TEST_KEY_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(envVarName, TestKeyBase64);

        try
        {
            var options = new SchemaSignatureOptions
            {
                KeySource = SchemaSignatureKeySource.EnvironmentVariable,
                KeyReference = envVarName,
            };

            var key = options.ResolveKey();

            Assert.Equal(TestKey, key);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void Options_ResolveKey_FromFile()
    {
        var keyPath = Path.Combine(_tempDir, "signing.key");
        File.WriteAllBytes(keyPath, TestKey);

        var options = new SchemaSignatureOptions
        {
            KeySource = SchemaSignatureKeySource.File,
            KeyReference = keyPath,
        };

        var key = options.ResolveKey();

        Assert.Equal(TestKey, key);
    }

    [Fact]
    public void Options_ResolveKey_MissingEnvVar_ThrowsInvalidOperation()
    {
        var options = new SchemaSignatureOptions
        {
            KeySource = SchemaSignatureKeySource.EnvironmentVariable,
            KeyReference = "NONEXISTENT_ENV_VAR_FOR_TEST",
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.ResolveKey());
        Assert.Contains("not set", ex.Message);
    }

    [Fact]
    public void Options_ResolveKey_MissingFile_ThrowsInvalidOperation()
    {
        var options = new SchemaSignatureOptions
        {
            KeySource = SchemaSignatureKeySource.File,
            KeyReference = "/nonexistent/key.file",
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.ResolveKey());
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void Options_ResolveKey_EmptyFile_ThrowsInvalidOperation()
    {
        var keyPath = Path.Combine(_tempDir, "empty.key");
        File.WriteAllBytes(keyPath, []);

        var options = new SchemaSignatureOptions
        {
            KeySource = SchemaSignatureKeySource.File,
            KeyReference = keyPath,
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.ResolveKey());
        Assert.Contains("empty", ex.Message);
    }

    [Fact]
    public void Options_ResolveKey_EmptyKeyReference_ThrowsInvalidOperation()
    {
        var options = new SchemaSignatureOptions
        {
            KeySource = SchemaSignatureKeySource.EnvironmentVariable,
            KeyReference = "",
        };

        Assert.Throws<InvalidOperationException>(() => options.ResolveKey());
    }

    [Fact]
    public void Options_ResolveKey_AzureKeyVault_ThrowsNotSupported()
    {
        var options = new SchemaSignatureOptions
        {
            KeySource = SchemaSignatureKeySource.AzureKeyVault,
            KeyReference = "https://vault.azure.net/secrets/signing-key",
        };

        Assert.Throws<NotSupportedException>(() => options.ResolveKey());
    }

    [Fact]
    public void Options_DefaultKeySource_IsEnvironmentVariable()
    {
        var options = new SchemaSignatureOptions();

        Assert.Equal(SchemaSignatureKeySource.EnvironmentVariable, options.KeySource);
        Assert.Equal("ALL_SCHEMA_SIGNING_KEY", options.KeyReference);
    }

    // ── SchemaVerificationResult — Result Type ──────────────────────────

    [Fact]
    public void VerificationResult_Success_IsValidWithNoError()
    {
        var result = SchemaVerificationResult.Success();

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Fact]
    public void VerificationResult_Failure_IsNotValidWithError()
    {
        var result = SchemaVerificationResult.Failure("Test error");

        Assert.False(result.IsValid);
        Assert.Equal("Test error", result.Error);
    }

    // ── Edge Cases ──────────────────────────────────────────────────────

    [Fact]
    public void ComputeSignature_EmptyContent_ProducesValidSignature()
    {
        var signature = SchemaSigner.ComputeSignature([], TestKey);

        Assert.Equal(64, signature.Length);
        Assert.Matches("^[0-9a-f]{64}$", signature);
    }

    [Fact]
    public void Verify_SingleByteChange_Detected()
    {
        var content = Encoding.UTF8.GetBytes(SampleSchema);
        var signature = SchemaSigner.ComputeSignature(content, TestKey);

        // Change one byte
        var tampered = (byte[])content.Clone();
        tampered[0] ^= 0x01;

        var result = SchemaVerifier.Verify(tampered, signature, TestKey);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Verify_InvalidHexSignature_ThrowsFormatException()
    {
        var content = Encoding.UTF8.GetBytes(SampleSchema);

        // "zzzz" is not valid hex
        Assert.ThrowsAny<FormatException>(
            () => SchemaVerifier.Verify(content, "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz", TestKey));
    }

    [Fact]
    public void SignFile_OverwritesExistingSigFile()
    {
        var schemaPath = WriteTempSchema(SampleSchema);

        // Sign once
        var result1 = SchemaSigner.SignFile(schemaPath, TestKey);

        // Modify and sign again
        File.WriteAllText(schemaPath, SampleSchema + "\n# updated");
        var result2 = SchemaSigner.SignFile(schemaPath, TestKey);

        Assert.NotEqual(result1.Signature, result2.Signature);
        Assert.True(SchemaVerifier.VerifyFile(schemaPath, TestKey).IsValid);
    }

    // ── Integration: Options → Sign → Verify ────────────────────────────

    [Fact]
    public void EndToEnd_OptionsSignVerify_WithEnvVar()
    {
        var envVarName = $"ALL_TEST_KEY_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(envVarName, TestKeyBase64);

        try
        {
            var options = new SchemaSignatureOptions
            {
                KeySource = SchemaSignatureKeySource.EnvironmentVariable,
                KeyReference = envVarName,
            };
            var key = options.ResolveKey();
            var schemaPath = WriteTempSchema(SampleSchema);

            SchemaSigner.SignFile(schemaPath, key);
            var result = SchemaVerifier.VerifyFile(schemaPath, key);

            Assert.True(result.IsValid);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void EndToEnd_OptionsSignVerify_WithKeyFile()
    {
        var keyPath = Path.Combine(_tempDir, "e2e.key");
        File.WriteAllBytes(keyPath, TestKey);

        var options = new SchemaSignatureOptions
        {
            KeySource = SchemaSignatureKeySource.File,
            KeyReference = keyPath,
        };
        var key = options.ResolveKey();
        var schemaPath = WriteTempSchema(SampleSchema);

        SchemaSigner.SignFile(schemaPath, key);
        var result = SchemaVerifier.VerifyFile(schemaPath, key);

        Assert.True(result.IsValid);
    }
}
