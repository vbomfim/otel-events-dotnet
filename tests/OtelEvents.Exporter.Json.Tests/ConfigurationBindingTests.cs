using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.Exporter.Json.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsJsonExporterOptions"/> and <see cref="OtelEventsSeverityFilterOptions"/>
/// binding from <see cref="IConfiguration"/> (appsettings.json / environment variables).
/// Covers Feature 2.2 from the specification.
/// </summary>
public sealed class ConfigurationBindingTests
{
    // ─── OtelEventsJsonExporterOptions Binding ──────────────────────────────

    [Fact]
    public void ExporterOptions_BindsOutputEnum_FromConfiguration()
    {
        var config = BuildConfig(("All:Exporter:Output", "Stderr"));

        var options = BindExporterOptions(config);

        Assert.Equal(OtelEventsJsonOutput.Stderr, options.Output);
    }

    [Fact]
    public void ExporterOptions_BindsEnvironmentProfile_FromConfiguration()
    {
        var config = BuildConfig(("All:Exporter:EnvironmentProfile", "Development"));

        var options = BindExporterOptions(config);

        Assert.Equal(OtelEventsEnvironmentProfile.Development, options.EnvironmentProfile);
    }

    [Fact]
    public void ExporterOptions_BindsEmitHostInfo_FromConfiguration()
    {
        var config = BuildConfig(("All:Exporter:EmitHostInfo", "true"));

        var options = BindExporterOptions(config);

        Assert.True(options.EmitHostInfo);
    }

    [Fact]
    public void ExporterOptions_BindsMaxAttributeValueLength_FromConfiguration()
    {
        var config = BuildConfig(("All:Exporter:MaxAttributeValueLength", "8192"));

        var options = BindExporterOptions(config);

        Assert.Equal(8192, options.MaxAttributeValueLength);
    }

    [Fact]
    public void ExporterOptions_BindsSchemaVersion_FromConfiguration()
    {
        var config = BuildConfig(("All:Exporter:SchemaVersion", "2.0.0"));

        var options = BindExporterOptions(config);

        Assert.Equal("2.0.0", options.SchemaVersion);
    }

    [Fact]
    public void ExporterOptions_BindsFilePath_FromConfiguration()
    {
        var config = BuildConfig(
            ("All:Exporter:Output", "File"),
            ("All:Exporter:FilePath", "/var/log/app.jsonl"));

        var options = BindExporterOptions(config);

        Assert.Equal(OtelEventsJsonOutput.File, options.Output);
        Assert.Equal("/var/log/app.jsonl", options.FilePath);
    }

    [Fact]
    public void ExporterOptions_BindsExceptionDetailLevel_FromConfiguration()
    {
        var config = BuildConfig(("All:Exporter:ExceptionDetailLevel", "TypeOnly"));

        var options = BindExporterOptions(config);

        Assert.Equal(ExceptionDetailLevel.TypeOnly, options.ExceptionDetailLevel);
    }

    [Fact]
    public void ExporterOptions_PreservesDefaults_WhenSectionEmpty()
    {
        var config = BuildConfig(); // empty configuration

        var options = BindExporterOptions(config);

        Assert.Equal(OtelEventsJsonOutput.Stdout, options.Output);
        Assert.Equal(OtelEventsEnvironmentProfile.Production, options.EnvironmentProfile);
        Assert.False(options.EmitHostInfo);
        Assert.Equal(4096, options.MaxAttributeValueLength);
        Assert.Equal("1.0.0", options.SchemaVersion);
        Assert.Null(options.FilePath);
        Assert.Null(options.ExceptionDetailLevel);
    }

    [Theory]
    [InlineData("Stdout", OtelEventsJsonOutput.Stdout)]
    [InlineData("Stderr", OtelEventsJsonOutput.Stderr)]
    [InlineData("File", OtelEventsJsonOutput.File)]
    public void ExporterOptions_BindsAllOutputEnumValues(string configValue, OtelEventsJsonOutput expected)
    {
        var config = BuildConfig(("All:Exporter:Output", configValue));

        var options = BindExporterOptions(config);

        Assert.Equal(expected, options.Output);
    }

    [Theory]
    [InlineData("Development", OtelEventsEnvironmentProfile.Development)]
    [InlineData("Staging", OtelEventsEnvironmentProfile.Staging)]
    [InlineData("Production", OtelEventsEnvironmentProfile.Production)]
    public void ExporterOptions_BindsOtelEventsEnvironmentProfileValues(
        string configValue, OtelEventsEnvironmentProfile expected)
    {
        var config = BuildConfig(("All:Exporter:EnvironmentProfile", configValue));

        var options = BindExporterOptions(config);

        Assert.Equal(expected, options.EnvironmentProfile);
    }

    // ─── OtelEventsSeverityFilterOptions Binding ────────────────────────────

    [Fact]
    public void FilterOptions_BindsMinSeverity_FromConfiguration()
    {
        var config = BuildConfig(("All:Filter:MinSeverity", "Warning"));

        var options = BindFilterOptions(config);

        Assert.Equal(LogLevel.Warning, options.MinSeverity);
    }

    [Fact]
    public void FilterOptions_PreservesDefault_WhenSectionEmpty()
    {
        var config = BuildConfig(); // empty configuration

        var options = BindFilterOptions(config);

        Assert.Equal(LogLevel.Information, options.MinSeverity);
    }

    [Theory]
    [InlineData("Trace", LogLevel.Trace)]
    [InlineData("Debug", LogLevel.Debug)]
    [InlineData("Information", LogLevel.Information)]
    [InlineData("Warning", LogLevel.Warning)]
    [InlineData("Error", LogLevel.Error)]
    [InlineData("Critical", LogLevel.Critical)]
    public void FilterOptions_BindsAllLogLevelValues(string configValue, LogLevel expected)
    {
        var config = BuildConfig(("All:Filter:MinSeverity", configValue));

        var options = BindFilterOptions(config);

        Assert.Equal(expected, options.MinSeverity);
    }

    // ─── EnvironmentProfile Auto-Detection ──────────────────────────

    [Fact]
    public void AutoDetects_Development_FromAspNetCoreEnvironment()
    {
        var result = EnvironmentProfileDetector.Detect(
            name => name == "ASPNETCORE_ENVIRONMENT" ? "Development" : null);

        Assert.Equal(OtelEventsEnvironmentProfile.Development, result);
    }

    [Fact]
    public void AutoDetects_Staging_FromDotNetEnvironment()
    {
        var result = EnvironmentProfileDetector.Detect(
            name => name == "DOTNET_ENVIRONMENT" ? "Staging" : null);

        Assert.Equal(OtelEventsEnvironmentProfile.Staging, result);
    }

    [Fact]
    public void AutoDetects_Production_FromAspNetCoreEnvironment()
    {
        var result = EnvironmentProfileDetector.Detect(
            name => name == "ASPNETCORE_ENVIRONMENT" ? "Production" : null);

        Assert.Equal(OtelEventsEnvironmentProfile.Production, result);
    }

    [Fact]
    public void AspNetCoreEnvironment_TakesPrecedence_OverDotNetEnvironment()
    {
        var result = EnvironmentProfileDetector.Detect(
            name => name switch
            {
                "ASPNETCORE_ENVIRONMENT" => "Development",
                "DOTNET_ENVIRONMENT" => "Staging",
                _ => null,
            });

        Assert.Equal(OtelEventsEnvironmentProfile.Development, result);
    }

    [Fact]
    public void DefaultsToProduction_WhenNoEnvironmentVarSet()
    {
        var result = EnvironmentProfileDetector.Detect(_ => null);

        Assert.Equal(OtelEventsEnvironmentProfile.Production, result);
    }

    [Fact]
    public void DefaultsToProduction_WhenUnknownEnvironmentValue()
    {
        var result = EnvironmentProfileDetector.Detect(
            name => name == "ASPNETCORE_ENVIRONMENT" ? "CustomEnv" : null);

        Assert.Equal(OtelEventsEnvironmentProfile.Production, result);
    }

    [Fact]
    public void AutoDetection_IsCaseInsensitive()
    {
        var result = EnvironmentProfileDetector.Detect(
            name => name == "ASPNETCORE_ENVIRONMENT" ? "development" : null);

        Assert.Equal(OtelEventsEnvironmentProfile.Development, result);
    }

    [Fact]
    public void ExplicitConfig_Overrides_AutoDetection()
    {
        // Arrange — config explicitly sets Development
        var config = BuildConfig(("All:Exporter:EnvironmentProfile", "Development"));

        // Act — apply with auto-detection that would return Staging
        var options = BindExporterOptionsWithAutoDetection(
            config,
            name => name == "DOTNET_ENVIRONMENT" ? "Staging" : null);

        // Assert — explicit config wins over auto-detection
        Assert.Equal(OtelEventsEnvironmentProfile.Development, options.EnvironmentProfile);
    }

    [Fact]
    public void AutoDetection_AppliesWhenProfileNotInConfig()
    {
        // Arrange — config does NOT set EnvironmentProfile
        var config = BuildConfig(("All:Exporter:Output", "Stderr"));

        // Act — apply with auto-detection that returns Development
        var options = BindExporterOptionsWithAutoDetection(
            config,
            name => name == "ASPNETCORE_ENVIRONMENT" ? "Development" : null);

        // Assert — auto-detection fills in the profile
        Assert.Equal(OtelEventsEnvironmentProfile.Development, options.EnvironmentProfile);
    }

    // ─── Environment Variable Override Pattern ──────────────────────

    [Fact]
    public void EnvironmentVariables_Override_InMemoryConfiguration()
    {
        // Demonstrates that env var keys (ALL__Exporter__Output) map to
        // configuration paths (All:Exporter:Output) when using
        // ConfigurationBuilder.AddEnvironmentVariables().
        // Here we simulate the resolved config with in-memory values.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["All:Exporter:Output"] = "Stdout",
                ["All:Exporter:MaxAttributeValueLength"] = "2048",
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Second source overrides first (simulates env vars)
                ["All:Exporter:Output"] = "Stderr",
            })
            .Build();

        var options = BindExporterOptions(config);

        // Env var override takes effect
        Assert.Equal(OtelEventsJsonOutput.Stderr, options.Output);
        // Non-overridden value preserved from first source
        Assert.Equal(2048, options.MaxAttributeValueLength);
    }

    // ─── DI Extension Overloads ─────────────────────────────────────

    [Fact]
    public void AddOtelEventsJsonExporter_WithConfiguration_RegistersPipeline()
    {
        // Arrange
        var config = BuildConfig(("All:Exporter:Output", "Stderr"));

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                builder.AddOtelEventsJsonExporter(config);
            });

        // Act & Assert — pipeline builds without errors
        using var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("test");
        logger.LogInformation("test message");

        // If we get here without exceptions, the pipeline is correctly registered
    }

    [Fact]
    public void AddAllSeverityFilter_WithConfiguration_RegistersPipeline()
    {
        // Arrange
        var config = BuildConfig(("All:Filter:MinSeverity", "Warning"));
        var exportedRecords = new List<LogLevel>();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                var exporter = new InMemoryLogExporter(exportedRecords);
                var exportProcessor = new SimpleLogRecordExportProcessor(exporter);

                builder.AddAllSeverityFilter(config, exportProcessor);
            });

        using var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        // Act
        var logger = loggerFactory.CreateLogger("test");
        logger.LogDebug("debug");
        logger.LogInformation("info");
        logger.LogWarning("warning");
        logger.LogError("error");

        loggerFactory.Dispose();

        // Assert — only Warning and above pass through
        Assert.Equal(2, exportedRecords.Count);
    }

    [Fact]
    public void AddOtelEventsJsonExporter_NullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    builder.AddOtelEventsJsonExporter((IConfiguration)null!));
            });
    }

    [Fact]
    public void AddAllSeverityFilter_NullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    builder.AddAllSeverityFilter(
                        (IConfiguration)null!,
                        new InMemoryLogRecordProcessor()));
            });
    }

    [Fact]
    public void AddOtelEventsJsonExporter_NullBuilder_WithConfiguration_ThrowsArgumentNullException()
    {
        LoggerProviderBuilder? nullBuilder = null;
        var config = BuildConfig();

        Assert.Throws<ArgumentNullException>(() =>
            nullBuilder!.AddOtelEventsJsonExporter(config));
    }

    [Fact]
    public void AddAllSeverityFilter_NullBuilder_WithConfiguration_ThrowsArgumentNullException()
    {
        LoggerProviderBuilder? nullBuilder = null;
        var config = BuildConfig();

        Assert.Throws<ArgumentNullException>(() =>
            nullBuilder!.AddAllSeverityFilter(config, new InMemoryLogRecordProcessor()));
    }

    // ─── Full Configuration Scenario ────────────────────────────────

    [Fact]
    public void FullConfiguration_BindsAllScalarProperties()
    {
        var config = BuildConfig(
            ("All:Exporter:Output", "File"),
            ("All:Exporter:FilePath", "/var/log/events.jsonl"),
            ("All:Exporter:SchemaVersion", "2.0.0"),
            ("All:Exporter:EnvironmentProfile", "Staging"),
            ("All:Exporter:ExceptionDetailLevel", "Full"),
            ("All:Exporter:EmitHostInfo", "true"),
            ("All:Exporter:MaxAttributeValueLength", "8192"));

        var options = BindExporterOptions(config);

        Assert.Equal(OtelEventsJsonOutput.File, options.Output);
        Assert.Equal("/var/log/events.jsonl", options.FilePath);
        Assert.Equal("2.0.0", options.SchemaVersion);
        Assert.Equal(OtelEventsEnvironmentProfile.Staging, options.EnvironmentProfile);
        Assert.Equal(ExceptionDetailLevel.Full, options.ExceptionDetailLevel);
        Assert.True(options.EmitHostInfo);
        Assert.Equal(8192, options.MaxAttributeValueLength);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="IConfiguration"/> from in-memory key-value pairs.
    /// Keys use the colon separator (e.g., "All:Exporter:Output").
    /// </summary>
    private static IConfiguration BuildConfig(params (string Key, string Value)[] values)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in values)
        {
            dict[key] = value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    /// <summary>
    /// Binds <see cref="OtelEventsJsonExporterOptions"/> from the "All:Exporter" section.
    /// Mirrors the production binding logic.
    /// </summary>
    private static OtelEventsJsonExporterOptions BindExporterOptions(IConfiguration config)
    {
        var options = new OtelEventsJsonExporterOptions();
        config.GetSection("All:Exporter").Bind(options);
        return options;
    }

    /// <summary>
    /// Binds <see cref="OtelEventsJsonExporterOptions"/> with EnvironmentProfile auto-detection.
    /// Mirrors the production DI extension logic.
    /// </summary>
    private static OtelEventsJsonExporterOptions BindExporterOptionsWithAutoDetection(
        IConfiguration config,
        Func<string, string?> getEnvironmentVariable)
    {
        var section = config.GetSection("All:Exporter");
        var options = new OtelEventsJsonExporterOptions();
        section.Bind(options);

        if (section["EnvironmentProfile"] is null)
        {
            options.EnvironmentProfile = EnvironmentProfileDetector.Detect(getEnvironmentVariable);
        }

        return options;
    }

    /// <summary>
    /// Binds <see cref="OtelEventsSeverityFilterOptions"/> from the "All:Filter" section.
    /// Mirrors the production binding logic.
    /// </summary>
    private static OtelEventsSeverityFilterOptions BindFilterOptions(IConfiguration config)
    {
        var options = new OtelEventsSeverityFilterOptions();
        config.GetSection("All:Filter").Bind(options);
        return options;
    }

    /// <summary>
    /// Minimal in-memory exporter that captures LogLevel of exported records.
    /// </summary>
    private sealed class InMemoryLogExporter(List<LogLevel> records) : BaseExporter<LogRecord>
    {
        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            foreach (var record in batch)
            {
                records.Add(record.LogLevel);
            }

            return ExportResult.Success;
        }
    }
}
