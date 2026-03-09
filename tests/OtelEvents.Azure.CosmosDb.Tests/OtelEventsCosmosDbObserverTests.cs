using System.Diagnostics;
using OtelEvents.Azure.CosmosDb.Events;

namespace OtelEvents.Azure.CosmosDb.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsCosmosDbObserver"/> — the DiagnosticListener observer
/// that captures CosmosDB operations and emits structured events.
/// Tests use mock DiagnosticListener payloads matching the Azure.Cosmos SDK shape.
/// </summary>
public sealed class OtelEventsCosmosDbObserverTests : IDisposable
{
    private readonly TestLogger<OtelEventsCosmosDbEventSource> _logger;
    private readonly OtelEventsCosmosDbOptions _options;

    public OtelEventsCosmosDbObserverTests()
    {
        _logger = new TestLogger<OtelEventsCosmosDbEventSource>();
        _options = new OtelEventsCosmosDbOptions();
    }

    private OtelEventsCosmosDbObserver CreateObserver(OtelEventsCosmosDbOptions? options = null)
        => new(_logger, options ?? _options);

    public void Dispose()
    {
        // No-op — observers created per test don't subscribe to global listeners
    }

    // ─── DiagnosticListener subscription tests ──────────────────────

    [Fact]
    public void OnNext_DiagnosticListener_SubscribesToCosmosOperationSource()
    {
        // Arrange
        var observer = CreateObserver();
        using var listener = new DiagnosticListener("Azure.Cosmos.Operation");

        // Act — simulate the observer discovering the CosmosDB listener
        observer.OnNext(listener);

        // Assert — observer should now be subscribed (verify by firing an event)
        if (listener.IsEnabled("Azure.Cosmos.Operation.Stop"))
        {
            listener.Write("Azure.Cosmos.Operation.Stop", CreateQueryPayload());
        }

        var entries = _logger.GetEntriesByEventName("cosmosdb.query.executed");
        Assert.Single(entries);
    }

    [Fact]
    public void OnNext_DiagnosticListener_IgnoresNonCosmosListeners()
    {
        // Arrange
        var observer = CreateObserver();
        using var listener = new DiagnosticListener("SomeOther.Source");

        // Act
        observer.OnNext(listener);

        // Assert — no subscription, firing events should not produce log entries
        if (listener.IsEnabled("SomeOther.Source.Stop"))
        {
            listener.Write("SomeOther.Source.Stop", CreateQueryPayload());
        }

        Assert.Empty(_logger.Entries);
    }

    // ─── cosmosdb.query.executed event tests ────────────────────────

    [Fact]
    public void ProcessEvent_QueryExecuted_EmitsCorrectEventId()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateQueryPayload();

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.executed").Single();
        Assert.Equal(10201, entry.EventId.Id);
        Assert.Equal("cosmosdb.query.executed", entry.EventId.Name);
    }

    [Fact]
    public void ProcessEvent_QueryExecuted_HasDebugSeverity()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateQueryPayload();

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.executed").Single();
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Debug, entry.LogLevel);
    }

    [Fact]
    public void ProcessEvent_QueryExecuted_IncludesDatabaseAndContainer()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateQueryPayload(database: "OrderDb", container: "Orders");

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.executed").Single();
        Assert.Equal("OrderDb", entry.Parameters["cosmosDatabase"]);
        Assert.Equal("Orders", entry.Parameters["cosmosContainer"]);
    }

    [Fact]
    public void ProcessEvent_QueryExecuted_IncludesRequestCharge()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateQueryPayload(requestCharge: 42.5);

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.executed").Single();
        Assert.Equal(42.5, entry.Parameters["cosmosRequestCharge"]);
    }

    [Fact]
    public void ProcessEvent_QueryExecuted_IncludesItemCount()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateQueryPayload(itemCount: 15);

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.executed").Single();
        Assert.Equal(15, entry.Parameters["cosmosItemCount"]);
    }

    [Fact]
    public void ProcessEvent_QueryExecuted_IncludesDuration()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateQueryPayload(durationMs: 23.4);

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.executed").Single();
        Assert.Equal(23.4, entry.Parameters["durationMs"]);
    }

    [Fact]
    public void ProcessEvent_QueryExecuted_IncludesStatusCode()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateQueryPayload(statusCode: 200);

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.executed").Single();
        Assert.Equal(200, entry.Parameters["cosmosStatusCode"]);
    }

    [Fact]
    public void ProcessEvent_QueryExecuted_IncludesRegion_WhenCaptureRegionEnabled()
    {
        // Arrange
        var options = new OtelEventsCosmosDbOptions { CaptureRegion = true };
        var observer = CreateObserver(options);
        var payload = CreateQueryPayload(region: "East US");

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.executed").Single();
        Assert.Equal("East US", entry.Parameters["cosmosRegion"]);
    }

    [Fact]
    public void ProcessEvent_QueryExecuted_ExcludesRegion_WhenCaptureRegionDisabled()
    {
        // Arrange
        var options = new OtelEventsCosmosDbOptions { CaptureRegion = false };
        var observer = CreateObserver(options);
        var payload = CreateQueryPayload(region: "East US");

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.executed").Single();
        Assert.Null(entry.Parameters["cosmosRegion"]);
    }

    [Fact]
    public void ProcessEvent_QueryExecuted_IncludesQueryText_WhenCaptureEnabled()
    {
        // Arrange
        var options = new OtelEventsCosmosDbOptions { CaptureQueryText = true };
        var observer = CreateObserver(options);
        var payload = CreateQueryPayload(queryText: "SELECT * FROM c WHERE c.name = 'test'");

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.executed").Single();
        // Query text should be sanitized — string literal replaced with ?
        Assert.Equal("SELECT * FROM c WHERE c.name = ?", entry.Parameters["cosmosQueryText"]);
    }

    [Fact]
    public void ProcessEvent_QueryExecuted_ExcludesQueryText_WhenCaptureDisabled()
    {
        // Arrange — CaptureQueryText defaults to false
        var observer = CreateObserver();
        var payload = CreateQueryPayload(queryText: "SELECT * FROM c WHERE c.name = 'test'");

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.executed").Single();
        Assert.Null(entry.Parameters["cosmosQueryText"]);
    }

    [Fact]
    public void ProcessEvent_QueryExecuted_SanitizesQueryText()
    {
        // Arrange
        var options = new OtelEventsCosmosDbOptions { CaptureQueryText = true };
        var observer = CreateObserver(options);
        var payload = CreateQueryPayload(
            queryText: "SELECT * FROM c WHERE c.email = 'user@example.com' AND c.ssn = '123-45-6789'");

        // Act
        FireStopEvent(observer, payload);

        // Assert — PII should be replaced with ?
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.executed").Single();
        var sanitized = (string?)entry.Parameters["cosmosQueryText"];
        Assert.NotNull(sanitized);
        Assert.DoesNotContain("user@example.com", sanitized);
        Assert.DoesNotContain("123-45-6789", sanitized);
        Assert.Contains("?", sanitized);
    }

    // ─── cosmosdb.point.read event tests ────────────────────────────

    [Fact]
    public void ProcessEvent_PointRead_EmitsCorrectEvent()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreatePointReadPayload();

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.point.read").Single();
        Assert.Equal(10203, entry.EventId.Id);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Debug, entry.LogLevel);
    }

    [Fact]
    public void ProcessEvent_PointRead_IncludesPartitionKey()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreatePointReadPayload(partitionKey: "customer-42");

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.point.read").Single();
        Assert.Equal("customer-42", entry.Parameters["cosmosPartitionKey"]);
    }

    [Fact]
    public void ProcessEvent_PointRead_IncludesRequestCharge()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreatePointReadPayload(requestCharge: 1.0);

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.point.read").Single();
        Assert.Equal(1.0, entry.Parameters["cosmosRequestCharge"]);
    }

    // ─── cosmosdb.point.write event tests ───────────────────────────

    [Fact]
    public void ProcessEvent_PointWrite_CreateItem_EmitsCorrectEvent()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreatePointWritePayload(operationType: "CreateItem");

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.point.write").Single();
        Assert.Equal(10204, entry.EventId.Id);
    }

    [Fact]
    public void ProcessEvent_PointWrite_UpsertItem_EmitsCorrectEvent()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreatePointWritePayload(operationType: "UpsertItem");

        // Act
        FireStopEvent(observer, payload);

        // Assert
        Assert.Single(_logger.GetEntriesByEventName("cosmosdb.point.write"));
    }

    [Fact]
    public void ProcessEvent_PointWrite_ReplaceItem_EmitsCorrectEvent()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreatePointWritePayload(operationType: "ReplaceItem");

        // Act
        FireStopEvent(observer, payload);

        // Assert
        Assert.Single(_logger.GetEntriesByEventName("cosmosdb.point.write"));
    }

    [Fact]
    public void ProcessEvent_PointWrite_IncludesPartitionKey()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreatePointWritePayload(partitionKey: "order-99");

        // Act
        FireStopEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.point.write").Single();
        Assert.Equal("order-99", entry.Parameters["cosmosPartitionKey"]);
    }

    // ─── cosmosdb.query.failed event tests ──────────────────────────

    [Fact]
    public void ProcessEvent_QueryFailed_EmitsCorrectEvent()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateFailedPayload();

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.failed").Single();
        Assert.Equal(10202, entry.EventId.Id);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, entry.LogLevel);
    }

    [Fact]
    public void ProcessEvent_QueryFailed_IncludesStatusCode()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateFailedPayload(statusCode: 429);

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.failed").Single();
        Assert.Equal(429, entry.Parameters["cosmosStatusCode"]);
    }

    [Fact]
    public void ProcessEvent_QueryFailed_IncludesSubStatusCode()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateFailedPayload(statusCode: 429, subStatusCode: 3200);

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.failed").Single();
        Assert.Equal(3200, entry.Parameters["cosmosSubStatusCode"]);
    }

    [Fact]
    public void ProcessEvent_QueryFailed_IncludesErrorType()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateFailedPayload(errorType: "CosmosException");

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.failed").Single();
        Assert.Equal("CosmosException", entry.Parameters["errorType"]);
    }

    [Fact]
    public void ProcessEvent_QueryFailed_IncludesException()
    {
        // Arrange
        var observer = CreateObserver();
        var exception = new InvalidOperationException("Request rate is large");
        var payload = CreateFailedPayload(exception: exception);

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.query.failed").Single();
        Assert.Same(exception, entry.Exception);
    }

    // ─── Threshold filtering tests ──────────────────────────────────

    [Fact]
    public void ProcessEvent_BelowRuThreshold_DoesNotEmit()
    {
        // Arrange
        var options = new OtelEventsCosmosDbOptions { RuThreshold = 10.0 };
        var observer = CreateObserver(options);
        var payload = CreateQueryPayload(requestCharge: 5.0);

        // Act
        FireStopEvent(observer, payload);

        // Assert
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void ProcessEvent_AboveRuThreshold_Emits()
    {
        // Arrange
        var options = new OtelEventsCosmosDbOptions { RuThreshold = 10.0 };
        var observer = CreateObserver(options);
        var payload = CreateQueryPayload(requestCharge: 42.5);

        // Act
        FireStopEvent(observer, payload);

        // Assert
        Assert.Single(_logger.GetEntriesByEventName("cosmosdb.query.executed"));
    }

    [Fact]
    public void ProcessEvent_BelowLatencyThreshold_DoesNotEmit()
    {
        // Arrange
        var options = new OtelEventsCosmosDbOptions { LatencyThresholdMs = 100.0 };
        var observer = CreateObserver(options);
        var payload = CreateQueryPayload(durationMs: 23.4);

        // Act
        FireStopEvent(observer, payload);

        // Assert
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void ProcessEvent_AboveLatencyThreshold_Emits()
    {
        // Arrange
        var options = new OtelEventsCosmosDbOptions { LatencyThresholdMs = 100.0 };
        var observer = CreateObserver(options);
        var payload = CreateQueryPayload(durationMs: 150.0);

        // Act
        FireStopEvent(observer, payload);

        // Assert
        Assert.Single(_logger.GetEntriesByEventName("cosmosdb.query.executed"));
    }

    // ─── Error resilience tests ─────────────────────────────────────

    [Fact]
    public void ProcessEvent_NullPayload_DoesNotThrow()
    {
        // Arrange
        var observer = CreateObserver();

        // Act & Assert — should silently skip
        var record = Record.Exception(() =>
            FireStopEvent(observer, null));
        Assert.Null(record);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void ProcessEvent_MissingProperties_DoesNotThrow()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = new { SomeUnrelatedProperty = "value" };

        // Act & Assert — should silently handle missing properties
        var record = Record.Exception(() =>
            FireStopEvent(observer, payload));
        Assert.Null(record);
    }

    [Fact]
    public void OnError_DoesNotThrow()
    {
        // Arrange
        var observer = CreateObserver();

        // Act & Assert
        var record = Record.Exception(() =>
            observer.OnError(new Exception("test")));
        Assert.Null(record);
    }

    [Fact]
    public void OnCompleted_DoesNotThrow()
    {
        // Arrange
        var observer = CreateObserver();

        // Act & Assert
        var record = Record.Exception(() =>
            observer.OnCompleted());
        Assert.Null(record);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var observer = CreateObserver();

        // Act & Assert
        var record = Record.Exception(() => observer.Dispose());
        Assert.Null(record);
    }

    // ─── Helper methods ─────────────────────────────────────────────

    /// <summary>
    /// Fires a "Stop" event (successful operation) through the observer.
    /// </summary>
    private static void FireStopEvent(OtelEventsCosmosDbObserver observer, object? payload)
    {
        ((IObserver<KeyValuePair<string, object?>>)observer).OnNext(
            new KeyValuePair<string, object?>("Azure.Cosmos.Operation.Stop", payload));
    }

    /// <summary>
    /// Fires an "Exception" event (failed operation) through the observer.
    /// </summary>
    private static void FireExceptionEvent(OtelEventsCosmosDbObserver observer, object? payload)
    {
        ((IObserver<KeyValuePair<string, object?>>)observer).OnNext(
            new KeyValuePair<string, object?>("Azure.Cosmos.Operation.Exception", payload));
    }

    /// <summary>
    /// Creates an anonymous payload mimicking a successful CosmosDB query operation.
    /// </summary>
    private static object CreateQueryPayload(
        string database = "TestDb",
        string container = "TestContainer",
        double requestCharge = 42.5,
        int itemCount = 15,
        double durationMs = 23.4,
        int statusCode = 200,
        string? region = "East US",
        string? partitionKey = null,
        string? queryText = null)
    {
        return new
        {
            DatabaseName = database,
            ContainerName = container,
            RequestCharge = requestCharge,
            ItemCount = itemCount,
            DurationMs = durationMs,
            StatusCode = statusCode,
            OperationType = "Query",
            Region = region,
            PartitionKey = partitionKey,
            QueryText = queryText,
        };
    }

    /// <summary>
    /// Creates an anonymous payload mimicking a successful CosmosDB point read.
    /// </summary>
    private static object CreatePointReadPayload(
        string database = "TestDb",
        string container = "TestContainer",
        double requestCharge = 1.0,
        double durationMs = 3.2,
        int statusCode = 200,
        string? region = "East US",
        string partitionKey = "pk-1")
    {
        return new
        {
            DatabaseName = database,
            ContainerName = container,
            RequestCharge = requestCharge,
            ItemCount = 1,
            DurationMs = durationMs,
            StatusCode = statusCode,
            OperationType = "ReadItem",
            Region = region,
            PartitionKey = partitionKey,
        };
    }

    /// <summary>
    /// Creates an anonymous payload mimicking a successful CosmosDB point write.
    /// </summary>
    private static object CreatePointWritePayload(
        string database = "TestDb",
        string container = "TestContainer",
        double requestCharge = 5.0,
        double durationMs = 8.1,
        int statusCode = 201,
        string? region = "East US",
        string partitionKey = "pk-1",
        string operationType = "CreateItem")
    {
        return new
        {
            DatabaseName = database,
            ContainerName = container,
            RequestCharge = requestCharge,
            ItemCount = 1,
            DurationMs = durationMs,
            StatusCode = statusCode,
            OperationType = operationType,
            Region = region,
            PartitionKey = partitionKey,
        };
    }

    /// <summary>
    /// Creates an anonymous payload mimicking a failed CosmosDB operation.
    /// </summary>
    private static object CreateFailedPayload(
        string database = "TestDb",
        string container = "TestContainer",
        double requestCharge = 0.0,
        double durationMs = 102.5,
        int statusCode = 429,
        int subStatusCode = 3200,
        string errorType = "CosmosException",
        string? partitionKey = null,
        Exception? exception = null)
    {
        return new
        {
            DatabaseName = database,
            ContainerName = container,
            RequestCharge = requestCharge,
            DurationMs = durationMs,
            StatusCode = statusCode,
            SubStatusCode = subStatusCode,
            ErrorType = errorType,
            PartitionKey = partitionKey,
            Exception = exception ?? new InvalidOperationException("Request rate is large"),
        };
    }
}
