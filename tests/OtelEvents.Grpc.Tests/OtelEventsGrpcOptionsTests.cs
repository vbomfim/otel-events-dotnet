using OtelEvents.Grpc;

namespace OtelEvents.Grpc.Tests;

/// <summary>
/// Unit tests for OtelEventsGrpcOptions — verifies all defaults
/// match the specification.
/// </summary>
public class OtelEventsGrpcOptionsTests
{
    [Fact]
    public void DefaultOptions_EnableCausalScope_IsTrue()
    {
        var options = new OtelEventsGrpcOptions();
        Assert.True(options.EnableCausalScope);
    }

    [Fact]
    public void DefaultOptions_EnableServerInterceptor_IsTrue()
    {
        var options = new OtelEventsGrpcOptions();
        Assert.True(options.EnableServerInterceptor);
    }

    [Fact]
    public void DefaultOptions_EnableClientInterceptor_IsTrue()
    {
        var options = new OtelEventsGrpcOptions();
        Assert.True(options.EnableClientInterceptor);
    }

    [Fact]
    public void DefaultOptions_CaptureMessageSize_IsTrue()
    {
        var options = new OtelEventsGrpcOptions();
        Assert.True(options.CaptureMessageSize);
    }

    [Fact]
    public void DefaultOptions_CaptureMetadata_IsFalse()
    {
        // Security: metadata may contain sensitive headers, default off
        var options = new OtelEventsGrpcOptions();
        Assert.False(options.CaptureMetadata);
    }

    [Fact]
    public void DefaultOptions_ExcludeServices_IsEmpty()
    {
        var options = new OtelEventsGrpcOptions();
        Assert.Empty(options.ExcludeServices);
    }

    [Fact]
    public void DefaultOptions_ExcludeMethods_IsEmpty()
    {
        var options = new OtelEventsGrpcOptions();
        Assert.Empty(options.ExcludeMethods);
    }

    [Fact]
    public void Options_CanBeConfigured()
    {
        // Arrange & Act
        var options = new OtelEventsGrpcOptions
        {
            EnableCausalScope = false,
            EnableServerInterceptor = false,
            EnableClientInterceptor = false,
            CaptureMessageSize = false,
            CaptureMetadata = true,
            ExcludeServices = ["grpc.health.v1.Health"],
            ExcludeMethods = ["/grpc.reflection.v1.ServerReflection/ServerReflectionInfo"]
        };

        // Assert
        Assert.False(options.EnableCausalScope);
        Assert.False(options.EnableServerInterceptor);
        Assert.False(options.EnableClientInterceptor);
        Assert.False(options.CaptureMessageSize);
        Assert.True(options.CaptureMetadata);
        Assert.Single(options.ExcludeServices);
        Assert.Single(options.ExcludeMethods);
    }
}
