namespace OtelEvents.AspNetCore.Tests;

/// <summary>
/// Tests for the embedded YAML schema resource.
/// Verifies the schema is properly bundled and accessible at runtime.
/// </summary>
public class EmbeddedSchemaTests
{
    [Fact]
    public void EmbeddedSchema_IsAccessible()
    {
        // Arrange
        var assembly = typeof(OtelEventsAspNetCoreOptions).Assembly;
        var resourceName = "OtelEvents.AspNetCore.aspnetcore.otel.yaml";

        // Act
        using var stream = assembly.GetManifestResourceStream(resourceName);

        // Assert
        Assert.NotNull(stream);
    }

    [Fact]
    public void EmbeddedSchema_ContainsExpectedEvents()
    {
        // Arrange
        var assembly = typeof(OtelEventsAspNetCoreOptions).Assembly;
        using var stream = assembly.GetManifestResourceStream("OtelEvents.AspNetCore.aspnetcore.otel.yaml")!;
        using var reader = new StreamReader(stream);

        // Act
        var content = reader.ReadToEnd();

        // Assert — verify key schema elements
        Assert.Contains("http.request.received", content);
        Assert.Contains("http.request.completed", content);
        Assert.Contains("http.request.failed", content);
        Assert.Contains("id: 10001", content);
        Assert.Contains("id: 10002", content);
        Assert.Contains("id: 10003", content);
        Assert.Contains("OtelEvents.AspNetCore", content);
    }

    [Fact]
    public void EmbeddedSchema_ContainsFieldDefinitions()
    {
        // Arrange
        var assembly = typeof(OtelEventsAspNetCoreOptions).Assembly;
        using var stream = assembly.GetManifestResourceStream("OtelEvents.AspNetCore.aspnetcore.otel.yaml")!;
        using var reader = new StreamReader(stream);

        // Act
        var content = reader.ReadToEnd();

        // Assert — verify key field definitions
        Assert.Contains("httpMethod", content);
        Assert.Contains("httpPath", content);
        Assert.Contains("httpStatusCode", content);
        Assert.Contains("durationMs", content);
        Assert.Contains("userAgent", content);
        Assert.Contains("clientIp", content);
        Assert.Contains("errorType", content);
    }

    [Fact]
    public void EmbeddedSchema_ContainsMetricDefinitions()
    {
        // Arrange
        var assembly = typeof(OtelEventsAspNetCoreOptions).Assembly;
        using var stream = assembly.GetManifestResourceStream("OtelEvents.AspNetCore.aspnetcore.otel.yaml")!;
        using var reader = new StreamReader(stream);

        // Act
        var content = reader.ReadToEnd();

        // Assert — verify metrics
        Assert.Contains("otel.http.request.received.count", content);
        Assert.Contains("otel.http.request.duration", content);
        Assert.Contains("otel.http.response.count", content);
        Assert.Contains("otel.http.request.error.count", content);
    }
}
