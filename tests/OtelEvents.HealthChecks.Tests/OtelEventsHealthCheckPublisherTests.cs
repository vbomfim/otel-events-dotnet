using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OtelEvents.HealthChecks.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsHealthCheckPublisher"/> — the core IHealthCheckPublisher
/// that emits health.check.executed, health.state.changed, and health.report.completed events.
/// </summary>
public sealed class OtelEventsHealthCheckPublisherTests
{
    private readonly TestLogger<OtelEventsHealthCheckPublisher> _logger;
    private readonly OtelEventsHealthCheckOptions _options;

    public OtelEventsHealthCheckPublisherTests()
    {
        _logger = new TestLogger<OtelEventsHealthCheckPublisher>();
        _options = new OtelEventsHealthCheckOptions();
    }

    private OtelEventsHealthCheckPublisher CreatePublisher(OtelEventsHealthCheckOptions? options = null)
        => new(_logger, options ?? _options);

    // ─── health.check.executed event tests ───────────────────────────

    [Fact]
    public async Task PublishAsync_EmitsHealthCheckExecuted_ForEachCheck()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.Create(
            ("Redis", HealthStatus.Healthy, TimeSpan.FromMilliseconds(12.4), null),
            ("CosmosDb", HealthStatus.Healthy, TimeSpan.FromMilliseconds(25.0), null));

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var executed = _logger.GetEntriesByEventName("health.check.executed");
        Assert.Equal(2, executed.Count);
    }

    [Fact]
    public async Task PublishAsync_HealthCheckExecuted_HasCorrectEventId()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.CreateHealthy();

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.check.executed").Single();
        Assert.Equal(10401, entry.EventId.Id);
        Assert.Equal("health.check.executed", entry.EventId.Name);
    }

    [Fact]
    public async Task PublishAsync_HealthCheckExecuted_HasDebugSeverity()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.CreateHealthy();

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.check.executed").Single();
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Debug, entry.LogLevel);
    }

    [Fact]
    public async Task PublishAsync_HealthCheckExecuted_IncludesComponentName()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.CreateHealthy("CosmosDb");

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.check.executed").Single();
        Assert.Equal("CosmosDb", entry.Parameters["healthComponent"]);
    }

    [Fact]
    public async Task PublishAsync_HealthCheckExecuted_IncludesStatusString()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.CreateDegraded("Redis");

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.check.executed").Single();
        Assert.Equal("Degraded", entry.Parameters["healthStatus"]);
    }

    [Fact]
    public async Task PublishAsync_HealthCheckExecuted_IncludesDurationMs()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.CreateHealthy(
            duration: TimeSpan.FromMilliseconds(42.5));

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.check.executed").Single();
        Assert.Equal(42.5, entry.Parameters["healthDurationMs"]);
    }

    [Fact]
    public async Task PublishAsync_HealthCheckExecuted_IncludesDescription_WhenProvided()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.CreateDegraded(
            description: "Connection pool exhausted");

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.check.executed").Single();
        Assert.Equal("Connection pool exhausted", entry.Parameters["healthDescription"]);
    }

    [Fact]
    public async Task PublishAsync_HealthCheckExecuted_DescriptionIsNull_WhenNotProvided()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.CreateHealthy(description: null);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.check.executed").Single();
        Assert.Null(entry.Parameters["healthDescription"]);
    }

    [Theory]
    [InlineData(HealthStatus.Healthy, "Healthy")]
    [InlineData(HealthStatus.Degraded, "Degraded")]
    [InlineData(HealthStatus.Unhealthy, "Unhealthy")]
    public async Task PublishAsync_HealthCheckExecuted_MapsAllStatusValues(
        HealthStatus status, string expected)
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.Create(
            ("TestCheck", status, TimeSpan.FromMilliseconds(10), null));

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.check.executed").Single();
        Assert.Equal(expected, entry.Parameters["healthStatus"]);
    }

    [Fact]
    public async Task PublishAsync_HealthCheckExecuted_MessageIncludesComponentStatusDuration()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.Create(
            ("Redis", HealthStatus.Healthy, TimeSpan.FromMilliseconds(12.4), null));

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.check.executed").Single();
        Assert.Contains("Redis", entry.Message);
        Assert.Contains("Healthy", entry.Message);
        Assert.Contains("12.4", entry.Message);
    }

    // ─── EmitExecutedEvents option tests ─────────────────────────────

    [Fact]
    public async Task PublishAsync_EmitExecutedEventsDisabled_DoesNotEmitExecuted()
    {
        // Arrange
        var options = new OtelEventsHealthCheckOptions { EmitExecutedEvents = false };
        var publisher = CreatePublisher(options);
        var report = HealthReportBuilder.CreateHealthy();

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var executed = _logger.GetEntriesByEventName("health.check.executed");
        Assert.Empty(executed);
    }

    [Fact]
    public async Task PublishAsync_SuppressHealthyExecutedEvents_SuppressesHealthyOnly()
    {
        // Arrange
        var options = new OtelEventsHealthCheckOptions { SuppressHealthyExecutedEvents = true };
        var publisher = CreatePublisher(options);
        var report = HealthReportBuilder.Create(
            ("Redis", HealthStatus.Healthy, TimeSpan.FromMilliseconds(10), null),
            ("CosmosDb", HealthStatus.Degraded, TimeSpan.FromMilliseconds(50), "Slow"),
            ("SqlServer", HealthStatus.Unhealthy, TimeSpan.FromMilliseconds(100), "Timeout"));

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert — only Degraded and Unhealthy emitted
        var executed = _logger.GetEntriesByEventName("health.check.executed");
        Assert.Equal(2, executed.Count);
        Assert.All(executed, e =>
            Assert.NotEqual("Healthy", e.Parameters["healthStatus"]));
    }

    [Fact]
    public async Task PublishAsync_SuppressHealthyExecutedEvents_EmitsNonHealthy()
    {
        // Arrange
        var options = new OtelEventsHealthCheckOptions { SuppressHealthyExecutedEvents = true };
        var publisher = CreatePublisher(options);
        var report = HealthReportBuilder.CreateDegraded("Redis", description: "Slow");

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var executed = _logger.GetEntriesByEventName("health.check.executed");
        Assert.Single(executed);
        Assert.Equal("Degraded", executed[0].Parameters["healthStatus"]);
    }

    // ─── health.state.changed event tests ────────────────────────────

    [Fact]
    public async Task PublishAsync_FirstPoll_RecordsState_NoStateChangedEvent()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.CreateHealthy("Redis");

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert — first poll records initial state without emitting state change
        var stateChanged = _logger.GetEntriesByEventName("health.state.changed");
        Assert.Empty(stateChanged);
    }

    [Fact]
    public async Task PublishAsync_SameStatus_NoStateChangedEvent()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.CreateHealthy("Redis");

        // Act — two polls with same status
        await publisher.PublishAsync(report, CancellationToken.None);
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var stateChanged = _logger.GetEntriesByEventName("health.state.changed");
        Assert.Empty(stateChanged);
    }

    [Fact]
    public async Task PublishAsync_StatusTransition_EmitsStateChanged()
    {
        // Arrange
        var publisher = CreatePublisher();
        var healthyReport = HealthReportBuilder.CreateHealthy("Redis");
        var degradedReport = HealthReportBuilder.CreateDegraded(
            "Redis", description: "Connection pool exhausted");

        // Act
        await publisher.PublishAsync(healthyReport, CancellationToken.None);
        await publisher.PublishAsync(degradedReport, CancellationToken.None);

        // Assert
        var stateChanged = _logger.GetEntriesByEventName("health.state.changed");
        Assert.Single(stateChanged);
    }

    [Fact]
    public async Task PublishAsync_StateChanged_HasCorrectEventId()
    {
        // Arrange
        var publisher = CreatePublisher();
        await publisher.PublishAsync(
            HealthReportBuilder.CreateHealthy("Redis"), CancellationToken.None);

        // Act
        await publisher.PublishAsync(
            HealthReportBuilder.CreateDegraded("Redis"), CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.state.changed").Single();
        Assert.Equal(10402, entry.EventId.Id);
        Assert.Equal("health.state.changed", entry.EventId.Name);
    }

    [Fact]
    public async Task PublishAsync_StateChanged_HasWarnSeverity()
    {
        // Arrange
        var publisher = CreatePublisher();
        await publisher.PublishAsync(
            HealthReportBuilder.CreateHealthy("Redis"), CancellationToken.None);

        // Act
        await publisher.PublishAsync(
            HealthReportBuilder.CreateDegraded("Redis"), CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.state.changed").Single();
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, entry.LogLevel);
    }

    [Fact]
    public async Task PublishAsync_StateChanged_IncludesPreviousAndCurrentStatus()
    {
        // Arrange
        var publisher = CreatePublisher();
        await publisher.PublishAsync(
            HealthReportBuilder.CreateHealthy("Redis"), CancellationToken.None);

        // Act
        await publisher.PublishAsync(
            HealthReportBuilder.CreateDegraded("Redis", description: "Slow"), CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.state.changed").Single();
        Assert.Equal("Redis", entry.Parameters["healthComponent"]);
        Assert.Equal("Healthy", entry.Parameters["healthPreviousStatus"]);
        Assert.Equal("Degraded", entry.Parameters["healthStatus"]);
    }

    [Fact]
    public async Task PublishAsync_StateChanged_IncludesDescription()
    {
        // Arrange
        var publisher = CreatePublisher();
        await publisher.PublishAsync(
            HealthReportBuilder.CreateHealthy("Redis"), CancellationToken.None);

        // Act
        await publisher.PublishAsync(
            HealthReportBuilder.CreateDegraded("Redis", description: "Connection pool exhausted"),
            CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.state.changed").Single();
        Assert.Equal("Connection pool exhausted", entry.Parameters["healthDescription"]);
    }

    [Fact]
    public async Task PublishAsync_StateChanged_RecoveryAlsoEmitsEvent()
    {
        // Arrange — degrade then recover
        var publisher = CreatePublisher();
        await publisher.PublishAsync(
            HealthReportBuilder.CreateHealthy("Redis"), CancellationToken.None);
        await publisher.PublishAsync(
            HealthReportBuilder.CreateDegraded("Redis"), CancellationToken.None);

        // Act — recover to Healthy
        await publisher.PublishAsync(
            HealthReportBuilder.CreateHealthy("Redis"), CancellationToken.None);

        // Assert — two state change events: Healthy→Degraded and Degraded→Healthy
        var stateChanged = _logger.GetEntriesByEventName("health.state.changed");
        Assert.Equal(2, stateChanged.Count);

        // Second event should be recovery
        Assert.Equal("Degraded", stateChanged[1].Parameters["healthPreviousStatus"]);
        Assert.Equal("Healthy", stateChanged[1].Parameters["healthStatus"]);
    }

    [Fact]
    public async Task PublishAsync_MultipleComponents_TracksStatesIndependently()
    {
        // Arrange
        var publisher = CreatePublisher();

        // First poll — both healthy
        await publisher.PublishAsync(HealthReportBuilder.Create(
            ("Redis", HealthStatus.Healthy, TimeSpan.FromMilliseconds(10), null),
            ("CosmosDb", HealthStatus.Healthy, TimeSpan.FromMilliseconds(20), null)),
            CancellationToken.None);

        // Act — only Redis degrades, CosmosDb stays healthy
        await publisher.PublishAsync(HealthReportBuilder.Create(
            ("Redis", HealthStatus.Degraded, TimeSpan.FromMilliseconds(50), "Slow"),
            ("CosmosDb", HealthStatus.Healthy, TimeSpan.FromMilliseconds(15), null)),
            CancellationToken.None);

        // Assert — only one state change for Redis
        var stateChanged = _logger.GetEntriesByEventName("health.state.changed");
        Assert.Single(stateChanged);
        Assert.Equal("Redis", stateChanged[0].Parameters["healthComponent"]);
    }

    // ─── EmitStateChangedEvents option tests ─────────────────────────

    [Fact]
    public async Task PublishAsync_EmitStateChangedEventsDisabled_DoesNotEmit()
    {
        // Arrange
        var options = new OtelEventsHealthCheckOptions { EmitStateChangedEvents = false };
        var publisher = CreatePublisher(options);

        await publisher.PublishAsync(
            HealthReportBuilder.CreateHealthy("Redis"), CancellationToken.None);

        // Act
        await publisher.PublishAsync(
            HealthReportBuilder.CreateDegraded("Redis"), CancellationToken.None);

        // Assert
        var stateChanged = _logger.GetEntriesByEventName("health.state.changed");
        Assert.Empty(stateChanged);
    }

    [Fact]
    public async Task PublishAsync_EmitStateChangedEventsDisabled_DoesNotTrackState()
    {
        // Arrange
        var options = new OtelEventsHealthCheckOptions { EmitStateChangedEvents = false };
        var publisher = CreatePublisher(options);

        // Act
        await publisher.PublishAsync(
            HealthReportBuilder.CreateHealthy("Redis"), CancellationToken.None);

        // Assert — no state tracking when disabled
        Assert.Equal(0, publisher.TrackedComponentCount);
    }

    // ─── health.report.completed event tests ─────────────────────────

    [Fact]
    public async Task PublishAsync_EmitsHealthReportCompleted()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.Create(
            ("Redis", HealthStatus.Healthy, TimeSpan.FromMilliseconds(10), null),
            ("CosmosDb", HealthStatus.Degraded, TimeSpan.FromMilliseconds(50), null));

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var completed = _logger.GetEntriesByEventName("health.report.completed");
        Assert.Single(completed);
    }

    [Fact]
    public async Task PublishAsync_HealthReportCompleted_HasCorrectEventId()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.CreateHealthy();

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.report.completed").Single();
        Assert.Equal(10403, entry.EventId.Id);
        Assert.Equal("health.report.completed", entry.EventId.Name);
    }

    [Fact]
    public async Task PublishAsync_HealthReportCompleted_HasDebugSeverity()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.CreateHealthy();

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.report.completed").Single();
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Debug, entry.LogLevel);
    }

    [Fact]
    public async Task PublishAsync_HealthReportCompleted_IncludesOverallStatus()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.Create(
            ("Redis", HealthStatus.Healthy, TimeSpan.FromMilliseconds(10), null),
            ("CosmosDb", HealthStatus.Degraded, TimeSpan.FromMilliseconds(50), null));

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert — overall status is Degraded (worst of all checks)
        var entry = _logger.GetEntriesByEventName("health.report.completed").Single();
        Assert.Equal("Degraded", entry.Parameters["healthOverallStatus"]);
    }

    [Fact]
    public async Task PublishAsync_HealthReportCompleted_IncludesTotalChecks()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.Create(
            ("Redis", HealthStatus.Healthy, TimeSpan.FromMilliseconds(10), null),
            ("CosmosDb", HealthStatus.Healthy, TimeSpan.FromMilliseconds(20), null),
            ("SqlServer", HealthStatus.Healthy, TimeSpan.FromMilliseconds(15), null));

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.report.completed").Single();
        Assert.Equal(3, entry.Parameters["healthTotalChecks"]);
    }

    [Fact]
    public async Task PublishAsync_HealthReportCompleted_IncludesTotalDurationMs()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.Create(
            ("Redis", HealthStatus.Healthy, TimeSpan.FromMilliseconds(10), null),
            ("CosmosDb", HealthStatus.Healthy, TimeSpan.FromMilliseconds(20), null));

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var entry = _logger.GetEntriesByEventName("health.report.completed").Single();
        Assert.Equal(report.TotalDuration.TotalMilliseconds, entry.Parameters["healthDurationMs"]);
    }

    // ─── EmitReportCompletedEvents option tests ──────────────────────

    [Fact]
    public async Task PublishAsync_EmitReportCompletedEventsDisabled_DoesNotEmit()
    {
        // Arrange
        var options = new OtelEventsHealthCheckOptions { EmitReportCompletedEvents = false };
        var publisher = CreatePublisher(options);
        var report = HealthReportBuilder.CreateHealthy();

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var completed = _logger.GetEntriesByEventName("health.report.completed");
        Assert.Empty(completed);
    }

    // ─── Bounded dictionary tests ────────────────────────────────────

    [Fact]
    public async Task PublishAsync_BoundedDictionary_RejectsAfterMaxCapacity()
    {
        // Arrange
        var publisher = CreatePublisher();

        // Fill up the dictionary to max capacity
        var entries = Enumerable.Range(0, OtelEventsHealthCheckPublisher.MaxTrackedComponents)
            .Select(i => ($"Check{i}", HealthStatus.Healthy, TimeSpan.FromMilliseconds(1), (string?)null))
            .ToArray();
        var fullReport = HealthReportBuilder.Create(entries);
        await publisher.PublishAsync(fullReport, CancellationToken.None);

        _logger.Clear();

        // Act — try to add one more component
        var overflowReport = HealthReportBuilder.Create(
            ("OverflowCheck", HealthStatus.Healthy, TimeSpan.FromMilliseconds(1), null));
        await publisher.PublishAsync(overflowReport, CancellationToken.None);

        // Assert — warning logged about capacity exceeded
        var warnings = _logger.GetEntriesByEventName("health.state.capacity.exceeded");
        Assert.Single(warnings);
        Assert.Equal(OtelEventsHealthCheckPublisher.MaxTrackedComponents,
            warnings[0].Parameters["capacity"]);
        Assert.Equal("OverflowCheck", warnings[0].Parameters["healthComponent"]);
    }

    [Fact]
    public async Task PublishAsync_BoundedDictionary_MaxCapacityIs1000()
    {
        // Assert — verify the constant matches spec
        Assert.Equal(1000, OtelEventsHealthCheckPublisher.MaxTrackedComponents);
    }

    [Fact]
    public async Task PublishAsync_BoundedDictionary_ExistingComponentsStillTracked()
    {
        // Arrange — fill to capacity
        var publisher = CreatePublisher();

        var entries = Enumerable.Range(0, OtelEventsHealthCheckPublisher.MaxTrackedComponents)
            .Select(i => ($"Check{i}", HealthStatus.Healthy, TimeSpan.FromMilliseconds(1), (string?)null))
            .ToArray();
        var fullReport = HealthReportBuilder.Create(entries);
        await publisher.PublishAsync(fullReport, CancellationToken.None);
        _logger.Clear();

        // Act — change state of an existing component (should still work)
        var changedReport = HealthReportBuilder.Create(
            ("Check0", HealthStatus.Degraded, TimeSpan.FromMilliseconds(50), "Degraded"));
        await publisher.PublishAsync(changedReport, CancellationToken.None);

        // Assert — state change detected for existing component
        var stateChanged = _logger.GetEntriesByEventName("health.state.changed");
        Assert.Single(stateChanged);
        Assert.Equal("Check0", stateChanged[0].Parameters["healthComponent"]);
    }

    // ─── All events disabled tests ───────────────────────────────────

    [Fact]
    public async Task PublishAsync_AllEventsDisabled_NoEventsEmitted()
    {
        // Arrange
        var options = new OtelEventsHealthCheckOptions
        {
            EmitExecutedEvents = false,
            EmitStateChangedEvents = false,
            EmitReportCompletedEvents = false,
        };
        var publisher = CreatePublisher(options);
        var report = HealthReportBuilder.Create(
            ("Redis", HealthStatus.Healthy, TimeSpan.FromMilliseconds(10), null));

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        Assert.Empty(_logger.Entries);
    }

    // ─── Null guard tests ────────────────────────────────────────────

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OtelEventsHealthCheckPublisher(null!, new OtelEventsHealthCheckOptions()));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OtelEventsHealthCheckPublisher(
                new TestLogger<OtelEventsHealthCheckPublisher>(), null!));
    }

    [Fact]
    public async Task PublishAsync_NullReport_ThrowsArgumentNullException()
    {
        var publisher = CreatePublisher();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            publisher.PublishAsync(null!, CancellationToken.None));
    }

    // ─── Multiple poll cycle tests ───────────────────────────────────

    [Fact]
    public async Task PublishAsync_MultiplePollCycles_EmitsExecutedEachTime()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.CreateHealthy("Redis");

        // Act — three poll cycles
        await publisher.PublishAsync(report, CancellationToken.None);
        await publisher.PublishAsync(report, CancellationToken.None);
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert — three executed events
        var executed = _logger.GetEntriesByEventName("health.check.executed");
        Assert.Equal(3, executed.Count);
    }

    [Fact]
    public async Task PublishAsync_MultiplePollCycles_EmitsReportCompletedEachTime()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.CreateHealthy("Redis");

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var completed = _logger.GetEntriesByEventName("health.report.completed");
        Assert.Equal(2, completed.Count);
    }

    [Fact]
    public async Task PublishAsync_MultipleTransitions_TracksLatestState()
    {
        // Arrange
        var publisher = CreatePublisher();

        // Act — Healthy → Degraded → Unhealthy → Healthy
        await publisher.PublishAsync(HealthReportBuilder.CreateHealthy("Redis"), CancellationToken.None);
        await publisher.PublishAsync(HealthReportBuilder.CreateDegraded("Redis"), CancellationToken.None);
        await publisher.PublishAsync(HealthReportBuilder.CreateUnhealthy("Redis"), CancellationToken.None);
        await publisher.PublishAsync(HealthReportBuilder.CreateHealthy("Redis"), CancellationToken.None);

        // Assert — three state changes
        var stateChanged = _logger.GetEntriesByEventName("health.state.changed");
        Assert.Equal(3, stateChanged.Count);

        // Verify transition chain
        Assert.Equal("Healthy", stateChanged[0].Parameters["healthPreviousStatus"]);
        Assert.Equal("Degraded", stateChanged[0].Parameters["healthStatus"]);

        Assert.Equal("Degraded", stateChanged[1].Parameters["healthPreviousStatus"]);
        Assert.Equal("Unhealthy", stateChanged[1].Parameters["healthStatus"]);

        Assert.Equal("Unhealthy", stateChanged[2].Parameters["healthPreviousStatus"]);
        Assert.Equal("Healthy", stateChanged[2].Parameters["healthStatus"]);
    }

    // ─── Empty report tests ─────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_EmptyReport_EmitsOnlyReportCompleted()
    {
        // Arrange
        var publisher = CreatePublisher();
        var report = HealthReportBuilder.Create(); // no entries

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert — no executed or state changed, but report completed
        var executed = _logger.GetEntriesByEventName("health.check.executed");
        var stateChanged = _logger.GetEntriesByEventName("health.state.changed");
        var completed = _logger.GetEntriesByEventName("health.report.completed");

        Assert.Empty(executed);
        Assert.Empty(stateChanged);
        Assert.Single(completed);
        Assert.Equal(0, completed[0].Parameters["healthTotalChecks"]);
    }
}
