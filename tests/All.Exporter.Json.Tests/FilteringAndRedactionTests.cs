using Microsoft.Extensions.Logging;

namespace All.Exporter.Json.Tests;

/// <summary>
/// Tests for allowlist/denylist attribute filtering and regex-based value redaction.
/// </summary>
public sealed class FilteringAndRedactionTests
{
    [Fact]
    public void Export_DenylistAttribute_IsExcluded()
    {
        using var harness = new TestExporterHarness(new AllJsonExporterOptions
        {
            AttributeDenylist = new HashSet<string> { "Password", "Token" },
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("Username", "john"),
                new("Password", "secret123"),
                new("Token", "tok_abc"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("john", attr.GetProperty("Username").GetString());
        Assert.False(attr.TryGetProperty("Password", out _));
        Assert.False(attr.TryGetProperty("Token", out _));
    }

    [Fact]
    public void Export_AllowlistAttribute_OnlyAllowedPass()
    {
        using var harness = new TestExporterHarness(new AllJsonExporterOptions
        {
            AttributeAllowlist = new HashSet<string> { "RequestPath", "StatusCode" },
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("RequestPath", "/api/orders"),
                new("StatusCode", 200),
                new("SensitiveData", "should be filtered"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("/api/orders", attr.GetProperty("RequestPath").GetString());
        Assert.Equal(200, attr.GetProperty("StatusCode").GetInt32());
        Assert.False(attr.TryGetProperty("SensitiveData", out _));
    }

    [Fact]
    public void Export_DenylistTakesPrecedenceOverAllowlist()
    {
        using var harness = new TestExporterHarness(new AllJsonExporterOptions
        {
            AttributeAllowlist = new HashSet<string> { "Username", "Password" },
            AttributeDenylist = new HashSet<string> { "Password" },
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("Username", "john"),
                new("Password", "secret123"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("john", attr.GetProperty("Username").GetString());
        Assert.False(attr.TryGetProperty("Password", out _));
    }

    [Fact]
    public void Export_NoAllowlistOrDenylist_AllAttributesPass()
    {
        using var harness = new TestExporterHarness(new AllJsonExporterOptions
        {
            // Default: no allowlist, empty denylist
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("field1", "value1"),
                new("field2", "value2"),
                new("field3", "value3"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("value1", attr.GetProperty("field1").GetString());
        Assert.Equal("value2", attr.GetProperty("field2").GetString());
        Assert.Equal("value3", attr.GetProperty("field3").GetString());
    }

    [Fact]
    public void Export_UserRedactPattern_RedactsMatchingValues()
    {
        using var harness = new TestExporterHarness(new AllJsonExporterOptions
        {
            RedactPatterns = [@"(?i)password\s*=\s*\S+"],
        });

        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("config", "password=secret123"),
                new("safe", "no sensitive data"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("[REDACTED]", attr.GetProperty("config").GetString());
        Assert.Equal("no sensitive data", attr.GetProperty("safe").GetString());
    }

    [Fact]
    public void Export_DefenseInDepth_ConnectionStringRedacted()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("connStr", "Server=myserver;User Id=admin;Password=s3cret"),
            ]);

        var doc = harness.ExportSingle(lr);

        Assert.Equal("[REDACTED:pattern]", doc.RootElement.GetProperty("attr").GetProperty("connStr").GetString());
    }

    [Fact]
    public void Export_DefenseInDepth_BearerTokenRedacted()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("authHeader", "Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9"),
            ]);

        var doc = harness.ExportSingle(lr);

        Assert.Equal("[REDACTED:pattern]", doc.RootElement.GetProperty("attr").GetProperty("authHeader").GetString());
    }

    [Fact]
    public void Export_DefenseInDepth_ApiKeyRedacted()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("config", "api_key=abcdefghijklmnopqrstuvwxyz1234567890"),
            ]);

        var doc = harness.ExportSingle(lr);

        Assert.Equal("[REDACTED:pattern]", doc.RootElement.GetProperty("attr").GetProperty("config").GetString());
    }

    [Fact]
    public void Export_SafeValue_NotRedacted()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(
            eventName: "test.event",
            attributes:
            [
                new("status", "Healthy"),
                new("count", "42"),
            ]);

        var doc = harness.ExportSingle(lr);

        var attr = doc.RootElement.GetProperty("attr");
        Assert.Equal("Healthy", attr.GetProperty("status").GetString());
        Assert.Equal("42", attr.GetProperty("count").GetString());
    }
}
