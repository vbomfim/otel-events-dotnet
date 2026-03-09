using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OtelEvents.Grpc;

namespace OtelEvents.Grpc.Tests;

/// <summary>
/// Tests for DI registration extensions (AddOtelEventsGrpc).
/// </summary>
public class OtelEventsGrpcExtensionsTests
{
    [Fact]
    public void AddOtelEventsGrpc_RegistersServerInterceptorInDI()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOtelEventsGrpc();
        var provider = services.BuildServiceProvider();

        // Assert — server interceptor should be resolvable
        var interceptor = provider.GetService<OtelEventsGrpcServerInterceptor>();
        Assert.NotNull(interceptor);
    }

    [Fact]
    public void AddOtelEventsGrpc_RegistersClientInterceptorInDI()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOtelEventsGrpc();
        var provider = services.BuildServiceProvider();

        // Assert — client interceptor should be resolvable
        var interceptor = provider.GetService<OtelEventsGrpcClientInterceptor>();
        Assert.NotNull(interceptor);
    }

    [Fact]
    public void AddOtelEventsGrpc_WithConfigure_AppliesOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOtelEventsGrpc(options =>
        {
            options.EnableCausalScope = false;
            options.ExcludeServices = ["grpc.health.v1.Health"];
        });
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OtelEventsGrpcOptions>>();

        // Assert
        Assert.False(options.Value.EnableCausalScope);
        Assert.Single(options.Value.ExcludeServices);
        Assert.Equal("grpc.health.v1.Health", options.Value.ExcludeServices[0]);
    }

    [Fact]
    public void AddOtelEventsGrpc_ThrowsOnNullServices()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddOtelEventsGrpc());
    }

    [Fact]
    public void AddOtelEventsGrpc_ThrowsOnNullConfigure()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddOtelEventsGrpc(null!));
    }
}
