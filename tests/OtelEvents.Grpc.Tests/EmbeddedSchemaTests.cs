namespace OtelEvents.Grpc.Tests;

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
        var assembly = typeof(OtelEventsGrpcOptions).Assembly;
        var resourceName = "OtelEvents.Grpc.grpc.all.yaml";

        // Act
        using var stream = assembly.GetManifestResourceStream(resourceName);

        // Assert
        Assert.NotNull(stream);
    }

    [Fact]
    public void EmbeddedSchema_ContainsExpectedEvents()
    {
        // Arrange
        var assembly = typeof(OtelEventsGrpcOptions).Assembly;
        using var stream = assembly.GetManifestResourceStream("OtelEvents.Grpc.grpc.all.yaml")!;
        using var reader = new StreamReader(stream);

        // Act
        var content = reader.ReadToEnd();

        // Assert — verify key schema elements
        Assert.Contains("grpc.call.started", content);
        Assert.Contains("grpc.call.completed", content);
        Assert.Contains("grpc.call.failed", content);
        Assert.Contains("id: 10101", content);
        Assert.Contains("id: 10102", content);
        Assert.Contains("id: 10103", content);
        Assert.Contains("OtelEvents.Grpc", content);
    }

    [Fact]
    public void EmbeddedSchema_ContainsFieldDefinitions()
    {
        // Arrange
        var assembly = typeof(OtelEventsGrpcOptions).Assembly;
        using var stream = assembly.GetManifestResourceStream("OtelEvents.Grpc.grpc.all.yaml")!;
        using var reader = new StreamReader(stream);

        // Act
        var content = reader.ReadToEnd();

        // Assert — verify key field definitions
        Assert.Contains("grpcService", content);
        Assert.Contains("grpcMethod", content);
        Assert.Contains("grpcStatusCode", content);
        Assert.Contains("durationMs", content);
        Assert.Contains("grpcSide", content);
        Assert.Contains("errorType", content);
    }

    [Fact]
    public void EmbeddedSchema_ContainsMetricDefinitions()
    {
        // Arrange
        var assembly = typeof(OtelEventsGrpcOptions).Assembly;
        using var stream = assembly.GetManifestResourceStream("OtelEvents.Grpc.grpc.all.yaml")!;
        using var reader = new StreamReader(stream);

        // Act
        var content = reader.ReadToEnd();

        // Assert — verify metrics
        Assert.Contains("otel.grpc.call.started.count", content);
        Assert.Contains("otel.grpc.call.duration", content);
        Assert.Contains("otel.grpc.call.completed.count", content);
        Assert.Contains("otel.grpc.call.error.count", content);
    }
}
