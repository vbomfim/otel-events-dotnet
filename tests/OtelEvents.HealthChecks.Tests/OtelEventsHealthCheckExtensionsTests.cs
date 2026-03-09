using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OtelEvents.HealthChecks.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsHealthCheckExtensions.AddOtelEventsHealthChecks"/>
/// DI registration and service resolution.
/// </summary>
public sealed class OtelEventsHealthCheckExtensionsTests
{
    [Fact]
    public void AddOtelEventsHealthChecks_RegistersPublisher()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOtelEventsHealthChecks();

        // Assert
        using var sp = services.BuildServiceProvider();
        var publisher = sp.GetService<IHealthCheckPublisher>();
        Assert.NotNull(publisher);
        Assert.IsType<OtelEventsHealthCheckPublisher>(publisher);
    }

    [Fact]
    public void AddOtelEventsHealthChecks_RegistersOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOtelEventsHealthChecks(opts =>
        {
            opts.EmitExecutedEvents = false;
            opts.SuppressHealthyExecutedEvents = true;
        });

        // Assert
        using var sp = services.BuildServiceProvider();
        var options = sp.GetService<OtelEventsHealthCheckOptions>();
        Assert.NotNull(options);
        Assert.False(options.EmitExecutedEvents);
        Assert.True(options.SuppressHealthyExecutedEvents);
    }

    [Fact]
    public void AddOtelEventsHealthChecks_DefaultOptions_AppliedWhenNoConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOtelEventsHealthChecks();

        // Assert
        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<OtelEventsHealthCheckOptions>();
        Assert.True(options.EmitExecutedEvents);
        Assert.True(options.EmitStateChangedEvents);
        Assert.True(options.EmitReportCompletedEvents);
        Assert.False(options.SuppressHealthyExecutedEvents);
        Assert.True(options.EnableCausalScope);
    }

    [Fact]
    public void AddOtelEventsHealthChecks_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? nullServices = null;

        Assert.Throws<ArgumentNullException>(() =>
            nullServices!.AddOtelEventsHealthChecks());
    }

    [Fact]
    public void AddOtelEventsHealthChecks_NullConfigure_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act — explicitly pass null configure action
        services.AddOtelEventsHealthChecks(configure: null);

        // Assert
        using var sp = services.BuildServiceProvider();
        var publisher = sp.GetService<IHealthCheckPublisher>();
        Assert.NotNull(publisher);
    }

    [Fact]
    public void AddOtelEventsHealthChecks_ReturnsSameServiceCollection_ForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        var result = services.AddOtelEventsHealthChecks();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void AddOtelEventsHealthChecks_PublisherIsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOtelEventsHealthChecks();

        // Act
        using var sp = services.BuildServiceProvider();
        var publisher1 = sp.GetRequiredService<IHealthCheckPublisher>();
        var publisher2 = sp.GetRequiredService<IHealthCheckPublisher>();

        // Assert — singleton returns same instance
        Assert.Same(publisher1, publisher2);
    }
}
