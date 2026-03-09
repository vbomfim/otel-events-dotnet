using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OtelEvents.Exporter.Json.Tests;

/// <summary>
/// Tests for exception serialization across different <see cref="ExceptionDetailLevel"/> settings
/// and <see cref="OtelEventsEnvironmentProfile"/> configurations.
/// </summary>
public sealed class ExceptionSerializationTests
{
    [Fact]
    public void Export_WithException_TypeAndMessage_IncludesTypeAndMessage()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            ExceptionDetailLevel = ExceptionDetailLevel.TypeAndMessage,
        });

        var ex = new InvalidOperationException("Something went wrong");
        var lr = TestExporterHarness.CreateLogRecord(
            logLevel: LogLevel.Error,
            eventName: "test.error",
            exception: ex);

        var doc = harness.ExportSingle(lr);

        var exObj = doc.RootElement.GetProperty("exception");
        Assert.Equal("System.InvalidOperationException", exObj.GetProperty("type").GetString());
        Assert.Equal("Something went wrong", exObj.GetProperty("message").GetString());
        Assert.False(exObj.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public void Export_WithException_TypeOnly_IncludesOnlyType()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            ExceptionDetailLevel = ExceptionDetailLevel.TypeOnly,
        });

        var ex = new ArgumentNullException("param", "Value cannot be null");
        var lr = TestExporterHarness.CreateLogRecord(
            logLevel: LogLevel.Error,
            eventName: "test.error",
            exception: ex);

        var doc = harness.ExportSingle(lr);

        var exObj = doc.RootElement.GetProperty("exception");
        Assert.Equal("System.ArgumentNullException", exObj.GetProperty("type").GetString());
        Assert.False(exObj.TryGetProperty("message", out _));
        Assert.False(exObj.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public void Export_WithException_Full_IncludesStackTrace()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            ExceptionDetailLevel = ExceptionDetailLevel.Full,
            EnvironmentProfile = OtelEventsEnvironmentProfile.Production,
        });

        Exception ex;
        try
        {
            throw new InvalidOperationException("Thrown for stack trace");
        }
        catch (Exception caught)
        {
            ex = caught;
        }

        var lr = TestExporterHarness.CreateLogRecord(
            logLevel: LogLevel.Error,
            eventName: "test.error",
            exception: ex);

        var doc = harness.ExportSingle(lr);

        var exObj = doc.RootElement.GetProperty("exception");
        Assert.Equal("System.InvalidOperationException", exObj.GetProperty("type").GetString());
        Assert.Equal("Thrown for stack trace", exObj.GetProperty("message").GetString());
        Assert.True(exObj.TryGetProperty("stackTrace", out var stackTrace));
        Assert.Equal(JsonValueKind.Array, stackTrace.ValueKind);
        Assert.True(stackTrace.GetArrayLength() > 0);

        // Production mode: no file paths
        var firstFrame = stackTrace[0];
        Assert.True(firstFrame.TryGetProperty("method", out _));
        Assert.False(firstFrame.TryGetProperty("file", out _));
        Assert.False(firstFrame.TryGetProperty("line", out _));
    }

    [Fact]
    public void Export_WithException_Full_Development_IncludesFilePaths()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            ExceptionDetailLevel = ExceptionDetailLevel.Full,
            EnvironmentProfile = OtelEventsEnvironmentProfile.Development,
        });

        Exception ex;
        try
        {
            throw new InvalidOperationException("Thrown for file info");
        }
        catch (Exception caught)
        {
            ex = caught;
        }

        var lr = TestExporterHarness.CreateLogRecord(
            logLevel: LogLevel.Error,
            eventName: "test.error",
            exception: ex);

        var doc = harness.ExportSingle(lr);

        var stackTrace = doc.RootElement.GetProperty("exception").GetProperty("stackTrace");
        Assert.True(stackTrace.GetArrayLength() > 0);

        // Development mode: file paths should be included
        var firstFrame = stackTrace[0];
        Assert.True(firstFrame.TryGetProperty("method", out _));
        // File info is available because the exception was thrown at runtime with debug info
        Assert.True(firstFrame.TryGetProperty("file", out _));
    }

    [Fact]
    public void Export_WithInnerException_IncludesNestedStructure()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            ExceptionDetailLevel = ExceptionDetailLevel.TypeAndMessage,
        });

        var innerEx = new ArgumentException("Bad argument");
        var outerEx = new InvalidOperationException("Outer error", innerEx);
        var lr = TestExporterHarness.CreateLogRecord(
            logLevel: LogLevel.Error,
            eventName: "test.error",
            exception: outerEx);

        var doc = harness.ExportSingle(lr);

        var exObj = doc.RootElement.GetProperty("exception");
        Assert.Equal("System.InvalidOperationException", exObj.GetProperty("type").GetString());

        var inner = exObj.GetProperty("inner");
        Assert.Equal("System.ArgumentException", inner.GetProperty("type").GetString());
        Assert.Equal("Bad argument", inner.GetProperty("message").GetString());
    }

    [Fact]
    public void Export_ExceptionDepthExceeds5_TruncatesWithFlag()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            ExceptionDetailLevel = ExceptionDetailLevel.TypeAndMessage,
        });

        // Build 6-deep exception chain
        Exception ex = new Exception("Level 6");
        for (int i = 5; i >= 1; i--)
        {
            ex = new Exception($"Level {i}", ex);
        }

        var lr = TestExporterHarness.CreateLogRecord(
            logLevel: LogLevel.Error,
            eventName: "test.error",
            exception: ex);

        var doc = harness.ExportSingle(lr);

        // Navigate down 5 levels
        var current = doc.RootElement.GetProperty("exception");
        Assert.Equal("Level 1", current.GetProperty("message").GetString());

        for (int i = 2; i <= 5; i++)
        {
            current = current.GetProperty("inner");
            Assert.Equal($"Level {i}", current.GetProperty("message").GetString());
        }

        // The 5th level's inner should be truncated
        var truncatedInner = current.GetProperty("inner");
        Assert.True(truncatedInner.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public void Export_NoException_OmitsExceptionField()
    {
        using var harness = new TestExporterHarness();
        var lr = TestExporterHarness.CreateLogRecord(eventName: "test.event");

        var doc = harness.ExportSingle(lr);

        Assert.False(doc.RootElement.TryGetProperty("exception", out _));
    }

    [Fact]
    public void Export_ProductionProfile_DefaultsToTypeAndMessage()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Production,
            // ExceptionDetailLevel NOT set — should default based on profile
        });

        var ex = new InvalidOperationException("Production error");
        var lr = TestExporterHarness.CreateLogRecord(
            logLevel: LogLevel.Error,
            eventName: "test.error",
            exception: ex);

        var doc = harness.ExportSingle(lr);

        var exObj = doc.RootElement.GetProperty("exception");
        Assert.True(exObj.TryGetProperty("type", out _));
        Assert.True(exObj.TryGetProperty("message", out _));
        Assert.False(exObj.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public void Export_DevelopmentProfile_DefaultsToFull()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Development,
            // ExceptionDetailLevel NOT set — should default to Full
        });

        Exception ex;
        try
        {
            throw new InvalidOperationException("Dev error");
        }
        catch (Exception caught)
        {
            ex = caught;
        }

        var lr = TestExporterHarness.CreateLogRecord(
            logLevel: LogLevel.Error,
            eventName: "test.error",
            exception: ex);

        var doc = harness.ExportSingle(lr);

        var exObj = doc.RootElement.GetProperty("exception");
        Assert.True(exObj.TryGetProperty("type", out _));
        Assert.True(exObj.TryGetProperty("message", out _));
        Assert.True(exObj.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public void Export_ExplicitExceptionDetailLevel_OverridesProfile()
    {
        using var harness = new TestExporterHarness(new OtelEventsJsonExporterOptions
        {
            EnvironmentProfile = OtelEventsEnvironmentProfile.Development,
            ExceptionDetailLevel = ExceptionDetailLevel.TypeOnly, // Override
        });

        var ex = new InvalidOperationException("Should only show type");
        var lr = TestExporterHarness.CreateLogRecord(
            logLevel: LogLevel.Error,
            eventName: "test.error",
            exception: ex);

        var doc = harness.ExportSingle(lr);

        var exObj = doc.RootElement.GetProperty("exception");
        Assert.True(exObj.TryGetProperty("type", out _));
        Assert.False(exObj.TryGetProperty("message", out _));
        Assert.False(exObj.TryGetProperty("stackTrace", out _));
    }
}
