namespace OtelEvents.Exporter.Json.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsJsonExporterOptions"/> defaults and configuration.
/// </summary>
public sealed class OtelEventsJsonExporterOptionsTests
{
    [Fact]
    public void DefaultOutput_IsStdout()
    {
        var options = new OtelEventsJsonExporterOptions();
        Assert.Equal(OtelEventsJsonOutput.Stdout, options.Output);
    }

    [Fact]
    public void DefaultSchemaVersion_Is100()
    {
        var options = new OtelEventsJsonExporterOptions();
        Assert.Equal("1.0.0", options.SchemaVersion);
    }

    [Fact]
    public void DefaultEnvironmentProfile_IsProduction()
    {
        var options = new OtelEventsJsonExporterOptions();
        Assert.Equal(OtelEventsEnvironmentProfile.Production, options.EnvironmentProfile);
    }

    [Fact]
    public void DefaultExceptionDetailLevel_IsNull()
    {
        var options = new OtelEventsJsonExporterOptions();
        Assert.Null(options.ExceptionDetailLevel);
    }

    [Fact]
    public void DefaultEmitHostInfo_IsFalse()
    {
        var options = new OtelEventsJsonExporterOptions();
        Assert.False(options.EmitHostInfo);
    }

    [Fact]
    public void DefaultMaxAttributeValueLength_Is4096()
    {
        var options = new OtelEventsJsonExporterOptions();
        Assert.Equal(4096, options.MaxAttributeValueLength);
    }

    [Fact]
    public void DefaultAttributeAllowlist_IsNull()
    {
        var options = new OtelEventsJsonExporterOptions();
        Assert.Null(options.AttributeAllowlist);
    }

    [Fact]
    public void DefaultAttributeDenylist_IsEmpty()
    {
        var options = new OtelEventsJsonExporterOptions();
        Assert.NotNull(options.AttributeDenylist);
        Assert.Empty(options.AttributeDenylist);
    }

    [Fact]
    public void DefaultRedactPatterns_IsEmpty()
    {
        var options = new OtelEventsJsonExporterOptions();
        Assert.NotNull(options.RedactPatterns);
        Assert.Empty(options.RedactPatterns);
    }

    [Fact]
    public void DefaultLockTimeout_Is100ms()
    {
        var options = new OtelEventsJsonExporterOptions();
        Assert.Equal(TimeSpan.FromMilliseconds(100), options.LockTimeout);
    }

    [Theory]
    [InlineData(OtelEventsEnvironmentProfile.Development, ExceptionDetailLevel.Full)]
    [InlineData(OtelEventsEnvironmentProfile.Staging, ExceptionDetailLevel.TypeAndMessage)]
    [InlineData(OtelEventsEnvironmentProfile.Production, ExceptionDetailLevel.TypeAndMessage)]
    public void ResolvedExceptionDetailLevel_FollowsProfile_WhenNotExplicitlySet(
        OtelEventsEnvironmentProfile profile, ExceptionDetailLevel expected)
    {
        var options = new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = profile,
            ExceptionDetailLevel = null,
        };

        Assert.Equal(expected, options.ResolvedExceptionDetailLevel);
    }

    [Fact]
    public void ResolvedExceptionDetailLevel_ExplicitOverride_TakesPrecedence()
    {
        var options = new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Production,
            ExceptionDetailLevel = ExceptionDetailLevel.Full,
        };

        Assert.Equal(ExceptionDetailLevel.Full, options.ResolvedExceptionDetailLevel);
    }
}
