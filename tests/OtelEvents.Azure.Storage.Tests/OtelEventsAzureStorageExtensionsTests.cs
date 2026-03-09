using Microsoft.Extensions.DependencyInjection;

namespace OtelEvents.Azure.Storage.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsAzureStorageExtensions"/> — DI registration.
/// Verifies correct service registration and option configuration.
/// </summary>
public sealed class OtelEventsAzureStorageExtensionsTests
{
    [Fact]
    public void AddOtelEventsAzureStorage_DefaultOverload_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddOtelEventsAzureStorage();

        var provider = services.BuildServiceProvider();
        var policy = provider.GetService<OtelEventsStoragePipelinePolicy>();
        Assert.NotNull(policy);
    }

    [Fact]
    public void AddOtelEventsAzureStorage_WithConfigure_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddOtelEventsAzureStorage(options =>
        {
            options.EnableBlobEvents = false;
            options.EnableQueueEvents = false;
        });

        var provider = services.BuildServiceProvider();
        var policy = provider.GetService<OtelEventsStoragePipelinePolicy>();
        Assert.NotNull(policy);
    }

    [Fact]
    public void AddOtelEventsAzureStorage_NullServices_Throws()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() =>
            services.AddOtelEventsAzureStorage());
    }

    [Fact]
    public void AddOtelEventsAzureStorage_NullConfigure_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddOtelEventsAzureStorage(null!));
    }

    [Fact]
    public void AddOtelEventsAzureStorage_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var result = services.AddOtelEventsAzureStorage();

        Assert.Same(services, result);
    }
}
