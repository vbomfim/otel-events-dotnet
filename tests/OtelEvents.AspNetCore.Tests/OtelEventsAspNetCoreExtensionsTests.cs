using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OtelEvents.AspNetCore;

namespace OtelEvents.AspNetCore.Tests;

/// <summary>
/// Tests for DI registration extensions (AddOtelEventsAspNetCore / UseOtelEventsAspNetCore).
/// </summary>
public class OtelEventsAspNetCoreExtensionsTests
{
    [Fact]
    public void AddOtelEventsAspNetCore_RegistersMiddlewareInDI()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOtelEventsAspNetCore();
        var provider = services.BuildServiceProvider();

        // Assert — middleware should be resolvable
        var middleware = provider.GetService<OtelEventsAspNetCoreMiddleware>();
        Assert.NotNull(middleware);
    }

    [Fact]
    public void AddOtelEventsAspNetCore_RegistersStartupFilter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOtelEventsAspNetCore();
        var provider = services.BuildServiceProvider();

        // Assert — startup filter should be registered
        var filter = provider.GetService<IStartupFilter>();
        Assert.NotNull(filter);
        Assert.IsType<OtelEventsAspNetCoreStartupFilter>(filter);
    }

    [Fact]
    public void AddOtelEventsAspNetCore_WithConfigure_AppliesOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOtelEventsAspNetCore(options =>
        {
            options.CaptureUserAgent = true;
            options.ExcludePaths = ["/health"];
        });
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OtelEventsAspNetCoreOptions>>();

        // Assert
        Assert.True(options.Value.CaptureUserAgent);
        Assert.Single(options.Value.ExcludePaths);
        Assert.Equal("/health", options.Value.ExcludePaths[0]);
    }

    [Fact]
    public void AddOtelEventsAspNetCore_ThrowsOnNullServices()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddOtelEventsAspNetCore());
    }

    [Fact]
    public void AddOtelEventsAspNetCore_ThrowsOnNullConfigure()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddOtelEventsAspNetCore(null!));
    }
}
