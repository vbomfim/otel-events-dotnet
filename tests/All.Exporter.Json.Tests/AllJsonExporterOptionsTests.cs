namespace All.Exporter.Json.Tests;

/// <summary>
/// Tests for <see cref="AllJsonExporterOptions"/> defaults and configuration.
/// </summary>
public sealed class AllJsonExporterOptionsTests
{
    [Fact]
    public void DefaultOutput_IsStdout()
    {
        var options = new AllJsonExporterOptions();
        Assert.Equal(AllJsonOutput.Stdout, options.Output);
    }

    [Fact]
    public void DefaultSchemaVersion_Is100()
    {
        var options = new AllJsonExporterOptions();
        Assert.Equal("1.0.0", options.SchemaVersion);
    }

    [Fact]
    public void DefaultEnvironmentProfile_IsProduction()
    {
        var options = new AllJsonExporterOptions();
        Assert.Equal(AllEnvironmentProfile.Production, options.EnvironmentProfile);
    }

    [Fact]
    public void DefaultExceptionDetailLevel_IsNull()
    {
        var options = new AllJsonExporterOptions();
        Assert.Null(options.ExceptionDetailLevel);
    }

    [Fact]
    public void DefaultEmitHostInfo_IsFalse()
    {
        var options = new AllJsonExporterOptions();
        Assert.False(options.EmitHostInfo);
    }

    [Fact]
    public void DefaultMaxAttributeValueLength_Is4096()
    {
        var options = new AllJsonExporterOptions();
        Assert.Equal(4096, options.MaxAttributeValueLength);
    }

    [Fact]
    public void DefaultAttributeAllowlist_IsNull()
    {
        var options = new AllJsonExporterOptions();
        Assert.Null(options.AttributeAllowlist);
    }

    [Fact]
    public void DefaultAttributeDenylist_IsEmpty()
    {
        var options = new AllJsonExporterOptions();
        Assert.NotNull(options.AttributeDenylist);
        Assert.Empty(options.AttributeDenylist);
    }

    [Fact]
    public void DefaultRedactPatterns_IsEmpty()
    {
        var options = new AllJsonExporterOptions();
        Assert.NotNull(options.RedactPatterns);
        Assert.Empty(options.RedactPatterns);
    }

    [Fact]
    public void DefaultLockTimeout_Is100ms()
    {
        var options = new AllJsonExporterOptions();
        Assert.Equal(TimeSpan.FromMilliseconds(100), options.LockTimeout);
    }

    [Theory]
    [InlineData(AllEnvironmentProfile.Development, ExceptionDetailLevel.Full)]
    [InlineData(AllEnvironmentProfile.Staging, ExceptionDetailLevel.TypeAndMessage)]
    [InlineData(AllEnvironmentProfile.Production, ExceptionDetailLevel.TypeAndMessage)]
    public void ResolvedExceptionDetailLevel_FollowsProfile_WhenNotExplicitlySet(
        AllEnvironmentProfile profile, ExceptionDetailLevel expected)
    {
        var options = new AllJsonExporterOptions
        {
            EnvironmentProfile = profile,
            ExceptionDetailLevel = null,
        };

        Assert.Equal(expected, options.ResolvedExceptionDetailLevel);
    }

    [Fact]
    public void ResolvedExceptionDetailLevel_ExplicitOverride_TakesPrecedence()
    {
        var options = new AllJsonExporterOptions
        {
            EnvironmentProfile = AllEnvironmentProfile.Production,
            ExceptionDetailLevel = ExceptionDetailLevel.Full,
        };

        Assert.Equal(ExceptionDetailLevel.Full, options.ResolvedExceptionDetailLevel);
    }
}
