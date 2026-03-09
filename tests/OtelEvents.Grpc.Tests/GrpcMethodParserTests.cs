using OtelEvents.Grpc;

namespace OtelEvents.Grpc.Tests;

/// <summary>
/// Unit tests for GrpcMethodParser — validates extraction of service name
/// and method name from gRPC method paths.
/// </summary>
public class GrpcMethodParserTests
{
    // ─── ExtractServiceName Tests ───────────────────────────────────────

    [Fact]
    public void ExtractServiceName_StandardPath_ReturnsServiceName()
    {
        var result = GrpcMethodParser.ExtractServiceName("/greet.Greeter/SayHello");
        Assert.Equal("greet.Greeter", result);
    }

    [Fact]
    public void ExtractServiceName_NoPackage_ReturnsServiceName()
    {
        var result = GrpcMethodParser.ExtractServiceName("/Greeter/SayHello");
        Assert.Equal("Greeter", result);
    }

    [Fact]
    public void ExtractServiceName_NestedPackage_ReturnsFullServiceName()
    {
        var result = GrpcMethodParser.ExtractServiceName("/com.example.api.v1.UserService/GetUser");
        Assert.Equal("com.example.api.v1.UserService", result);
    }

    [Fact]
    public void ExtractServiceName_NullInput_ReturnsUnknown()
    {
        var result = GrpcMethodParser.ExtractServiceName(null);
        Assert.Equal("unknown", result);
    }

    [Fact]
    public void ExtractServiceName_EmptyInput_ReturnsUnknown()
    {
        var result = GrpcMethodParser.ExtractServiceName("");
        Assert.Equal("unknown", result);
    }

    [Fact]
    public void ExtractServiceName_NoSlash_ReturnsUnknown()
    {
        var result = GrpcMethodParser.ExtractServiceName("invalid");
        Assert.Equal("unknown", result);
    }

    [Fact]
    public void ExtractServiceName_OnlySlash_ReturnsUnknown()
    {
        var result = GrpcMethodParser.ExtractServiceName("/");
        Assert.Equal("unknown", result);
    }

    // ─── ExtractMethodName Tests ────────────────────────────────────────

    [Fact]
    public void ExtractMethodName_StandardPath_ReturnsMethodName()
    {
        var result = GrpcMethodParser.ExtractMethodName("/greet.Greeter/SayHello");
        Assert.Equal("SayHello", result);
    }

    [Fact]
    public void ExtractMethodName_NoPackage_ReturnsMethodName()
    {
        var result = GrpcMethodParser.ExtractMethodName("/Greeter/SayHello");
        Assert.Equal("SayHello", result);
    }

    [Fact]
    public void ExtractMethodName_NullInput_ReturnsUnknown()
    {
        var result = GrpcMethodParser.ExtractMethodName(null);
        Assert.Equal("unknown", result);
    }

    [Fact]
    public void ExtractMethodName_EmptyInput_ReturnsUnknown()
    {
        var result = GrpcMethodParser.ExtractMethodName("");
        Assert.Equal("unknown", result);
    }

    [Fact]
    public void ExtractMethodName_TrailingSlashOnly_ReturnsUnknown()
    {
        var result = GrpcMethodParser.ExtractMethodName("/greet.Greeter/");
        Assert.Equal("unknown", result);
    }

    // ─── IsExcluded Tests ───────────────────────────────────────────────

    [Fact]
    public void IsExcluded_ServiceInExcludeList_ReturnsTrue()
    {
        var options = new OtelEventsGrpcOptions
        {
            ExcludeServices = ["grpc.health.v1.Health"]
        };

        var result = GrpcMethodParser.IsExcluded(
            "/grpc.health.v1.Health/Check", "grpc.health.v1.Health", options);

        Assert.True(result);
    }

    [Fact]
    public void IsExcluded_MethodInExcludeList_ReturnsTrue()
    {
        var options = new OtelEventsGrpcOptions
        {
            ExcludeMethods = ["/grpc.reflection.v1.ServerReflection/ServerReflectionInfo"]
        };

        var result = GrpcMethodParser.IsExcluded(
            "/grpc.reflection.v1.ServerReflection/ServerReflectionInfo",
            "grpc.reflection.v1.ServerReflection", options);

        Assert.True(result);
    }

    [Fact]
    public void IsExcluded_ServiceNotInList_ReturnsFalse()
    {
        var options = new OtelEventsGrpcOptions
        {
            ExcludeServices = ["grpc.health.v1.Health"]
        };

        var result = GrpcMethodParser.IsExcluded(
            "/greet.Greeter/SayHello", "greet.Greeter", options);

        Assert.False(result);
    }

    [Fact]
    public void IsExcluded_EmptyExcludeLists_ReturnsFalse()
    {
        var options = new OtelEventsGrpcOptions();

        var result = GrpcMethodParser.IsExcluded(
            "/greet.Greeter/SayHello", "greet.Greeter", options);

        Assert.False(result);
    }

    [Fact]
    public void IsExcluded_ServiceMatch_IsCaseInsensitive()
    {
        var options = new OtelEventsGrpcOptions
        {
            ExcludeServices = ["Grpc.Health.V1.Health"]
        };

        var result = GrpcMethodParser.IsExcluded(
            "/grpc.health.v1.Health/Check", "grpc.health.v1.Health", options);

        Assert.True(result);
    }
}
