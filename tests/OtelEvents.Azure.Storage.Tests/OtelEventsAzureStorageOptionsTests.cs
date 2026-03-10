namespace OtelEvents.Azure.Storage.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsAzureStorageOptions"/> default values.
/// Verifies that safe, spec-compliant defaults are applied.
/// </summary>
public sealed class OtelEventsAzureStorageOptionsTests
{
    [Fact]
    public void DefaultOptions_BlobEventsEnabled()
    {
        var options = new OtelEventsAzureStorageOptions();

        Assert.True(options.EnableBlobEvents);
    }

    [Fact]
    public void DefaultOptions_QueueEventsEnabled()
    {
        var options = new OtelEventsAzureStorageOptions();

        Assert.True(options.EnableQueueEvents);
    }

    [Fact]
    public void DefaultOptions_CausalScopeEnabled()
    {
        var options = new OtelEventsAzureStorageOptions();

        Assert.True(options.EnableCausalScope);
    }

    [Fact]
    public void DefaultOptions_ExcludeContainersEmpty()
    {
        var options = new OtelEventsAzureStorageOptions();

        Assert.NotNull(options.ExcludeContainers);
        Assert.Empty(options.ExcludeContainers);
    }

    [Fact]
    public void DefaultOptions_ExcludeQueuesEmpty()
    {
        var options = new OtelEventsAzureStorageOptions();

        Assert.NotNull(options.ExcludeQueues);
        Assert.Empty(options.ExcludeQueues);
    }

    [Fact]
    public void DefaultOptions_InfrastructureEventsEnabled()
    {
        var options = new OtelEventsAzureStorageOptions();

        Assert.True(options.EmitInfrastructureEvents);
    }

    [Fact]
    public void Options_CanConfigureAllProperties()
    {
        var options = new OtelEventsAzureStorageOptions
        {
            EnableBlobEvents = false,
            EnableQueueEvents = false,
            EnableCausalScope = false,
            EmitInfrastructureEvents = false,
            ExcludeContainers = ["logs", "diagnostics"],
            ExcludeQueues = ["internal-queue"]
        };

        Assert.False(options.EnableBlobEvents);
        Assert.False(options.EnableQueueEvents);
        Assert.False(options.EnableCausalScope);
        Assert.False(options.EmitInfrastructureEvents);
        Assert.Equal(2, options.ExcludeContainers.Count);
        Assert.Single(options.ExcludeQueues);
    }
}
