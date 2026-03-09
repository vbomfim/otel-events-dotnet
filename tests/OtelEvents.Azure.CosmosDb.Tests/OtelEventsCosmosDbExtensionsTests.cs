using Microsoft.Extensions.DependencyInjection;

namespace OtelEvents.Azure.CosmosDb.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsCosmosDbExtensions.AddOtelEventsCosmosDb"/>
/// DI registration and service resolution.
/// </summary>
public sealed class OtelEventsCosmosDbExtensionsTests
{
    [Fact]
    public void AddOtelEventsCosmosDb_RegistersObserver()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOtelEventsCosmosDb();

        // Assert
        using var sp = services.BuildServiceProvider();
        var observer = sp.GetService<OtelEventsCosmosDbObserver>();
        Assert.NotNull(observer);
    }

    [Fact]
    public void AddOtelEventsCosmosDb_RegistersOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOtelEventsCosmosDb(opts =>
        {
            opts.CaptureQueryText = true;
            opts.RuThreshold = 10.0;
        });

        // Assert
        using var sp = services.BuildServiceProvider();
        var options = sp.GetService<OtelEventsCosmosDbOptions>();
        Assert.NotNull(options);
        Assert.True(options.CaptureQueryText);
        Assert.Equal(10.0, options.RuThreshold);
    }

    [Fact]
    public void AddOtelEventsCosmosDb_DefaultOptions_AppliedWhenNoConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOtelEventsCosmosDb();

        // Assert
        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<OtelEventsCosmosDbOptions>();
        Assert.False(options.CaptureQueryText);
        Assert.True(options.EnableCausalScope);
        Assert.True(options.CaptureRegion);
        Assert.Equal(0, options.RuThreshold);
        Assert.Equal(0, options.LatencyThresholdMs);
    }

    [Fact]
    public void AddOtelEventsCosmosDb_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? nullServices = null;

        Assert.Throws<ArgumentNullException>(() =>
            nullServices!.AddOtelEventsCosmosDb());
    }

    [Fact]
    public void AddOtelEventsCosmosDb_NullConfigure_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act — explicitly pass null configure action
        services.AddOtelEventsCosmosDb(configure: null);

        // Assert
        using var sp = services.BuildServiceProvider();
        var observer = sp.GetService<OtelEventsCosmosDbObserver>();
        Assert.NotNull(observer);
    }

    [Fact]
    public void AddOtelEventsCosmosDb_ReturnsSameServiceCollection_ForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        var result = services.AddOtelEventsCosmosDb();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void AddOtelEventsCosmosDb_ObserverIsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOtelEventsCosmosDb();

        // Act
        using var sp = services.BuildServiceProvider();
        var observer1 = sp.GetRequiredService<OtelEventsCosmosDbObserver>();
        var observer2 = sp.GetRequiredService<OtelEventsCosmosDbObserver>();

        // Assert — singleton returns same instance
        Assert.Same(observer1, observer2);
    }
}
