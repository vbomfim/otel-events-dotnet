using OtelEvents.AspNetCore;

namespace OtelEvents.AspNetCore.Tests;

/// <summary>
/// Unit tests for OtelEventsAspNetCoreOptions — verifies all defaults
/// match the specification, especially PII-safe defaults.
/// </summary>
public class OtelEventsAspNetCoreOptionsTests
{
    [Fact]
    public void DefaultOptions_EnableCausalScope_IsTrue()
    {
        var options = new OtelEventsAspNetCoreOptions();
        Assert.True(options.EnableCausalScope);
    }

    [Fact]
    public void DefaultOptions_RecordRequestReceived_IsTrue()
    {
        var options = new OtelEventsAspNetCoreOptions();
        Assert.True(options.RecordRequestReceived);
    }

    [Fact]
    public void DefaultOptions_CaptureUserAgent_IsFalse()
    {
        // PII safety: must default to false per GDPR/CCPA
        var options = new OtelEventsAspNetCoreOptions();
        Assert.False(options.CaptureUserAgent);
    }

    [Fact]
    public void DefaultOptions_CaptureClientIp_IsFalse()
    {
        // PII safety: must default to false per GDPR/CCPA
        var options = new OtelEventsAspNetCoreOptions();
        Assert.False(options.CaptureClientIp);
    }

    [Fact]
    public void DefaultOptions_UseRouteTemplate_IsTrue()
    {
        var options = new OtelEventsAspNetCoreOptions();
        Assert.True(options.UseRouteTemplate);
    }

    [Fact]
    public void DefaultOptions_ExcludePaths_IsEmpty()
    {
        var options = new OtelEventsAspNetCoreOptions();
        Assert.Empty(options.ExcludePaths);
    }

    [Fact]
    public void DefaultOptions_MaxPathLength_Is256()
    {
        var options = new OtelEventsAspNetCoreOptions();
        Assert.Equal(256, options.MaxPathLength);
    }

    [Fact]
    public void Options_CanBeConfigured()
    {
        // Arrange & Act
        var options = new OtelEventsAspNetCoreOptions
        {
            EnableCausalScope = false,
            RecordRequestReceived = false,
            CaptureUserAgent = true,
            CaptureClientIp = true,
            UseRouteTemplate = false,
            ExcludePaths = ["/health", "/ready"],
            MaxPathLength = 512
        };

        // Assert
        Assert.False(options.EnableCausalScope);
        Assert.False(options.RecordRequestReceived);
        Assert.True(options.CaptureUserAgent);
        Assert.True(options.CaptureClientIp);
        Assert.False(options.UseRouteTemplate);
        Assert.Equal(2, options.ExcludePaths.Count);
        Assert.Equal(512, options.MaxPathLength);
    }
}
