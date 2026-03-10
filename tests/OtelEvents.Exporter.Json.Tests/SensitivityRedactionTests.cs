using Microsoft.Extensions.Logging;

namespace OtelEvents.Exporter.Json.Tests;

/// <summary>
/// Tests for sensitivity-based field redaction per EnvironmentProfile × sensitivity matrix.
/// Spec reference: SPECIFICATION.md §16.2 (PII Classification Framework).
/// </summary>
public sealed class SensitivityRedactionTests
{
    // ────────────────────────────────────────────────────────────────
    // ShouldRedact matrix tests — per profile × sensitivity level
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OtelEventsSensitivity.Public, false)]
    [InlineData(OtelEventsSensitivity.Internal, false)]
    [InlineData(OtelEventsSensitivity.Pii, false)]
    [InlineData(OtelEventsSensitivity.Credential, true)]
    public void ShouldRedact_Development_FollowsMatrix(OtelEventsSensitivity sensitivity, bool expected)
    {
        var result = SensitivityRegistry.ShouldRedact(sensitivity, OtelEventsEnvironmentProfile.Development);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(OtelEventsSensitivity.Public, false)]
    [InlineData(OtelEventsSensitivity.Internal, false)]
    [InlineData(OtelEventsSensitivity.Pii, true)]
    [InlineData(OtelEventsSensitivity.Credential, true)]
    public void ShouldRedact_Staging_FollowsMatrix(OtelEventsSensitivity sensitivity, bool expected)
    {
        var result = SensitivityRegistry.ShouldRedact(sensitivity, OtelEventsEnvironmentProfile.Staging);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(OtelEventsSensitivity.Public, false)]
    [InlineData(OtelEventsSensitivity.Internal, true)]
    [InlineData(OtelEventsSensitivity.Pii, true)]
    [InlineData(OtelEventsSensitivity.Credential, true)]
    public void ShouldRedact_Production_FollowsMatrix(OtelEventsSensitivity sensitivity, bool expected)
    {
        var result = SensitivityRegistry.ShouldRedact(sensitivity, OtelEventsEnvironmentProfile.Production);
        Assert.Equal(expected, result);
    }

    // ────────────────────────────────────────────────────────────────
    // SensitivityRegistry — built-in mappings
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("clientIp", OtelEventsSensitivity.Pii)]
    [InlineData("userAgent", OtelEventsSensitivity.Pii)]
    [InlineData("identityHint", OtelEventsSensitivity.Pii)]
    [InlineData("errorMessage", OtelEventsSensitivity.Internal)]
    [InlineData("endpoint", OtelEventsSensitivity.Internal)]
    [InlineData("hostName", OtelEventsSensitivity.Internal)]
    [InlineData("grpcStatusDetail", OtelEventsSensitivity.Internal)]
    [InlineData("cosmosQueryText", OtelEventsSensitivity.Internal)]
    public void Registry_BuiltInMappings_ReturnExpectedSensitivity(string fieldName, OtelEventsSensitivity expected)
    {
        var registry = new SensitivityRegistry();
        Assert.True(registry.TryGetSensitivity(fieldName, out var sensitivity));
        Assert.Equal(expected, sensitivity);
    }

    [Fact]
    public void Registry_UnknownField_ReturnsFalse()
    {
        var registry = new SensitivityRegistry();
        Assert.False(registry.TryGetSensitivity("unknownField", out _));
    }

    [Fact]
    public void Registry_Register_AddsCustomMapping()
    {
        var registry = new SensitivityRegistry();
        registry.Register("customField", OtelEventsSensitivity.Credential);

        Assert.True(registry.TryGetSensitivity("customField", out var sensitivity));
        Assert.Equal(OtelEventsSensitivity.Credential, sensitivity);
    }

    [Fact]
    public void Registry_Register_OverridesBuiltInMapping()
    {
        var registry = new SensitivityRegistry();
        registry.Register("clientIp", OtelEventsSensitivity.Public);

        Assert.True(registry.TryGetSensitivity("clientIp", out var sensitivity));
        Assert.Equal(OtelEventsSensitivity.Public, sensitivity);
    }

    [Fact]
    public void Registry_BuiltInMappings_CaseInsensitive()
    {
        var registry = new SensitivityRegistry();
        Assert.True(registry.TryGetSensitivity("CLIENTIP", out var sensitivity));
        Assert.Equal(OtelEventsSensitivity.Pii, sensitivity);
    }

    // ────────────────────────────────────────────────────────────────
    // Redacted value format: "[REDACTED:{level}]"
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OtelEventsSensitivity.Internal, "[REDACTED:internal]")]
    [InlineData(OtelEventsSensitivity.Pii, "[REDACTED:pii]")]
    [InlineData(OtelEventsSensitivity.Credential, "[REDACTED:credential]")]
    public void GetRedactedValue_ReturnsCorrectFormat(OtelEventsSensitivity sensitivity, string expected)
    {
        Assert.Equal(expected, SensitivityRegistry.GetRedactedValue(sensitivity));
    }

    // ────────────────────────────────────────────────────────────────
    // Export integration — sensitivity redaction in JSON output
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void Export_Production_RedactsPiiField()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Production,
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "http.request.completed",
            attributes:
            [
                new("clientIp", "192.168.1.100"),
                new("statusCode", 200),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("[REDACTED:pii]", attr.GetProperty("clientIp").GetString());
        Assert.Equal(200, attr.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public void Export_Production_RedactsInternalField()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Production,
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("errorMessage", "Connection refused"),
                new("endpoint", "https://internal-service:8443/api"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("[REDACTED:internal]", attr.GetProperty("errorMessage").GetString());
        Assert.Equal("[REDACTED:internal]", attr.GetProperty("endpoint").GetString());
    }

    [Fact]
    public void Export_Staging_AllowsInternalButRedactsPii()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Staging,
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("errorMessage", "Connection refused"),
                new("clientIp", "192.168.1.100"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("Connection refused", attr.GetProperty("errorMessage").GetString());
        Assert.Equal("[REDACTED:pii]", attr.GetProperty("clientIp").GetString());
    }

    [Fact]
    public void Export_Development_AllowsAllExceptCredential()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Development,
        });

        // Register a credential field for this test
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("clientIp", "192.168.1.100"),
                new("errorMessage", "Connection refused"),
                new("userAgent", "Mozilla/5.0"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("192.168.1.100", attr.GetProperty("clientIp").GetString());
        Assert.Equal("Connection refused", attr.GetProperty("errorMessage").GetString());
        Assert.Equal("Mozilla/5.0", attr.GetProperty("userAgent").GetString());
    }

    [Fact]
    public void Export_UnknownField_PassesThroughUnredacted()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Production,
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("customField", "some-value"),
                new("anotherField", "another-value"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("some-value", attr.GetProperty("customField").GetString());
        Assert.Equal("another-value", attr.GetProperty("anotherField").GetString());
    }

    // ────────────────────────────────────────────────────────────────
    // SensitivityOverrides — per-field opt-in/opt-out
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void Export_SensitivityOverride_TrueAllowsRedactedField()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Production,
            SensitivityOverrides = new Dictionary<string, bool>
            {
                ["clientIp"] = true, // Allow despite pii classification
            },
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("clientIp", "192.168.1.100"),
                new("userAgent", "Mozilla/5.0"), // No override — still redacted
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("192.168.1.100", attr.GetProperty("clientIp").GetString());
        Assert.Equal("[REDACTED:pii]", attr.GetProperty("userAgent").GetString());
    }

    [Fact]
    public void Export_SensitivityOverride_FalseRedactsVisibleField()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Staging,
            SensitivityOverrides = new Dictionary<string, bool>
            {
                ["hostName"] = false, // Force-redact despite internal being visible in Staging
            },
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("hostName", "web-server-01"),
                new("errorMessage", "Connection refused"), // internal — visible in Staging
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("[REDACTED:internal]", attr.GetProperty("hostName").GetString());
        Assert.Equal("Connection refused", attr.GetProperty("errorMessage").GetString());
    }

    [Fact]
    public void Export_SensitivityOverride_FalseOnUnknownField_Redacts()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Development,
            SensitivityOverrides = new Dictionary<string, bool>
            {
                ["customField"] = false, // Force-redact a field not in registry
            },
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("customField", "some-value"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("[REDACTED]", attr.GetProperty("customField").GetString());
    }

    // ────────────────────────────────────────────────────────────────
    // Non-string value redaction
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void Export_SensitivityRedaction_RedactsNonStringValues()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Production,
            SensitivityOverrides = new Dictionary<string, bool>
            {
                ["sensitiveCount"] = false, // Force-redact a numeric field
            },
        });

        // Register a custom field as credential so it gets redacted
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("sensitiveCount", 42),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        // Redacted values always become strings
        Assert.Equal("[REDACTED]", attr.GetProperty("sensitiveCount").GetString());
    }

    // ────────────────────────────────────────────────────────────────
    // Options defaults
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void Options_SensitivityOverrides_DefaultsToNull()
    {
        var options = new OtelEventsJsonExporterOptions();
        Assert.Null(options.SensitivityOverrides);
    }

    // ────────────────────────────────────────────────────────────────
    // Full pipeline integration test
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void Export_ProductionFullPipeline_RedactsCorrectFields()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Production,
            SensitivityOverrides = new Dictionary<string, bool>
            {
                ["identityHint"] = true, // Allow pii field for audit trail
            },
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "http.request.completed",
            message: "Request completed",
            attributes:
            [
                new("statusCode", 200),             // no sensitivity — passes through
                new("clientIp", "10.0.0.1"),         // pii — REDACTED
                new("userAgent", "curl/7.64.1"),     // pii — REDACTED
                new("identityHint", "user@test.com"),// pii — ALLOWED (override)
                new("errorMessage", "OK"),           // internal — REDACTED
                new("hostName", "web-01"),            // internal — REDACTED
                new("endpoint", "/api/v1/orders"),    // internal — REDACTED
                new("customField", "visible"),        // unknown — passes through
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");

        // Public / unknown → visible
        Assert.Equal(200, attr.GetProperty("statusCode").GetInt32());
        Assert.Equal("visible", attr.GetProperty("customField").GetString());

        // Pii → redacted
        Assert.Equal("[REDACTED:pii]", attr.GetProperty("clientIp").GetString());
        Assert.Equal("[REDACTED:pii]", attr.GetProperty("userAgent").GetString());

        // Pii with override → visible
        Assert.Equal("user@test.com", attr.GetProperty("identityHint").GetString());

        // Internal → redacted
        Assert.Equal("[REDACTED:internal]", attr.GetProperty("errorMessage").GetString());
        Assert.Equal("[REDACTED:internal]", attr.GetProperty("hostName").GetString());
        Assert.Equal("[REDACTED:internal]", attr.GetProperty("endpoint").GetString());
    }

    [Fact]
    public void Export_CredentialField_AlwaysRedacted()
    {
        // Register a custom credential field and verify it's redacted even in Development
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Development,
            SensitivityMappings = new Dictionary<string, OtelEventsSensitivity>
            {
                ["apiSecret"] = OtelEventsSensitivity.Credential,
            },
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("apiSecret", "super-secret-key-12345"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("[REDACTED:credential]", attr.GetProperty("apiSecret").GetString());
    }

    [Fact]
    public void Export_SensitivityMappings_RegisteredViaOptions()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Production,
            SensitivityMappings = new Dictionary<string, OtelEventsSensitivity>
            {
                ["customPiiField"] = OtelEventsSensitivity.Pii,
                ["publicField"] = OtelEventsSensitivity.Public,
            },
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("customPiiField", "sensitive-data"),
                new("publicField", "safe-data"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("[REDACTED:pii]", attr.GetProperty("customPiiField").GetString());
        Assert.Equal("safe-data", attr.GetProperty("publicField").GetString());
    }
}
