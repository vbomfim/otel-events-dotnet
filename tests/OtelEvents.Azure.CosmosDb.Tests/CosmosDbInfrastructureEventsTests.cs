using System.Diagnostics;
using OtelEvents.Azure.CosmosDb.Events;

namespace OtelEvents.Azure.CosmosDb.Tests;

/// <summary>
/// Tests for CosmosDB infrastructure events (10205–10207): connection failures,
/// auth failures, and throttling. Verifies the supplemental event model where
/// infrastructure events fire <em>in addition to</em> the existing query.failed event.
/// </summary>
public sealed class CosmosDbInfrastructureEventsTests : IDisposable
{
    private readonly TestLogger<OtelEventsCosmosDbEventSource> _logger;
    private readonly OtelEventsCosmosDbOptions _infraOptions;

    public CosmosDbInfrastructureEventsTests()
    {
        _logger = new TestLogger<OtelEventsCosmosDbEventSource>();
        _infraOptions = new OtelEventsCosmosDbOptions { EmitInfrastructureEvents = true };
    }

    private OtelEventsCosmosDbObserver CreateObserver(OtelEventsCosmosDbOptions? options = null)
        => new(_logger, options ?? _infraOptions);

    public void Dispose()
    {
        // No-op — observers created per test don't subscribe to global listeners
    }

    // ─── cosmosdb.connection.failed (10205) ─────────────────────────

    [Fact]
    public void ConnectionFailed_EmitsCorrectEventId_10205()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateConnectionFailedPayload();

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.connection.failed").Single();
        Assert.Equal(10205, entry.EventId.Id);
        Assert.Equal("cosmosdb.connection.failed", entry.EventId.Name);
    }

    [Fact]
    public void ConnectionFailed_HasErrorSeverity()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateConnectionFailedPayload();

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.connection.failed").Single();
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, entry.LogLevel);
    }

    [Fact]
    public void ConnectionFailed_IncludesEndpoint()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateConnectionFailedPayload(endpoint: "https://myaccount.documents.azure.com:443/");

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.connection.failed").Single();
        Assert.Equal("https://myaccount.documents.azure.com:443/", entry.Parameters["endpoint"]);
    }

    [Fact]
    public void ConnectionFailed_IncludesDatabaseAndDuration()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateConnectionFailedPayload(database: "OrderDb", durationMs: 5032.1);

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.connection.failed").Single();
        Assert.Equal("OrderDb", entry.Parameters["cosmosDatabase"]);
        Assert.Equal(5032.1, entry.Parameters["durationMs"]);
    }

    [Fact]
    public void ConnectionFailed_IncludesErrorTypeAndMessage()
    {
        // Arrange
        var observer = CreateObserver();
        var exception = new InvalidOperationException("Connection refused by remote host");
        var payload = CreateConnectionFailedPayload(
            errorType: "System.Net.Http.HttpRequestException",
            failureReason: "ConnectionRefused",
            exception: exception);

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.connection.failed").Single();
        Assert.Equal("System.Net.Http.HttpRequestException", entry.Parameters["errorType"]);
        Assert.Equal("Connection refused by remote host", entry.Parameters["errorMessage"]);
        Assert.Equal("ConnectionRefused", entry.Parameters["failureReason"]);
    }

    // ─── cosmosdb.auth.failed (10206) ───────────────────────────────

    [Fact]
    public void AuthFailed_EmitsCorrectEventId_10206()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateAuthFailedPayload(statusCode: 401);

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.auth.failed").Single();
        Assert.Equal(10206, entry.EventId.Id);
        Assert.Equal("cosmosdb.auth.failed", entry.EventId.Name);
    }

    [Fact]
    public void AuthFailed_HasErrorSeverity()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateAuthFailedPayload(statusCode: 401);

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.auth.failed").Single();
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, entry.LogLevel);
    }

    [Fact]
    public void AuthFailed_EmitsForStatus401()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateAuthFailedPayload(statusCode: 401);

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.auth.failed").Single();
        Assert.Equal(401, entry.Parameters["httpStatusCode"]);
    }

    [Fact]
    public void AuthFailed_EmitsForStatus403()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateAuthFailedPayload(statusCode: 403);

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.auth.failed").Single();
        Assert.Equal(403, entry.Parameters["httpStatusCode"]);
    }

    [Fact]
    public void AuthFailed_IncludesIdentityHint_SHA256Hash()
    {
        // Arrange
        var observer = CreateObserver();
        var exception = new InvalidOperationException("The input authorization token can't serve the request");
        var payload = CreateAuthFailedPayload(
            statusCode: 401,
            identitySource: "master-key-abc",
            exception: exception);

        // Act
        FireExceptionEvent(observer, payload);

        // Assert — identityHint should be a deterministic 8-char uppercase hex hash
        var entry = _logger.GetEntriesByEventName("cosmosdb.auth.failed").Single();
        var identityHint = (string?)entry.Parameters["identityHint"];
        Assert.NotNull(identityHint);
        Assert.Equal(8, identityHint.Length);
        Assert.Matches("^[0-9A-F]{8}$", identityHint);

        // Same input should produce same hash (deterministic)
        FireExceptionEvent(observer, CreateAuthFailedPayload(
            statusCode: 401, identitySource: "master-key-abc", exception: exception));
        var entries = _logger.GetEntriesByEventName("cosmosdb.auth.failed");
        var entry2 = entries[entries.Count - 1];
        Assert.Equal(identityHint, entry2.Parameters["identityHint"]);
    }

    // ─── cosmosdb.throttled (10207) ─────────────────────────────────

    [Fact]
    public void Throttled_EmitsCorrectEventId_10207()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateThrottledPayload();

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.throttled").Single();
        Assert.Equal(10207, entry.EventId.Id);
        Assert.Equal("cosmosdb.throttled", entry.EventId.Name);
    }

    [Fact]
    public void Throttled_HasWarnSeverity()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateThrottledPayload();

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.throttled").Single();
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, entry.LogLevel);
    }

    [Fact]
    public void Throttled_IncludesRetryAfterMs()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateThrottledPayload(retryAfterMs: 1234.0);

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.throttled").Single();
        Assert.Equal(1234.0, entry.Parameters["retryAfterMs"]);
    }

    [Fact]
    public void Throttled_IncludesRequestCharge()
    {
        // Arrange
        var observer = CreateObserver();
        var payload = CreateThrottledPayload(requestCharge: 99.9);

        // Act
        FireExceptionEvent(observer, payload);

        // Assert
        var entry = _logger.GetEntriesByEventName("cosmosdb.throttled").Single();
        Assert.Equal(99.9, entry.Parameters["cosmosRequestCharge"]);
    }

    // ─── Options & gating tests ─────────────────────────────────────

    [Fact]
    public void EmitInfrastructureEvents_DefaultsFalse()
    {
        // Arrange & Act
        var options = new OtelEventsCosmosDbOptions();

        // Assert — backward-compatible default
        Assert.False(options.EmitInfrastructureEvents);
    }

    [Fact]
    public void InfraEvents_NotEmitted_WhenDisabled()
    {
        // Arrange — EmitInfrastructureEvents = false (default)
        var options = new OtelEventsCosmosDbOptions();
        var observer = CreateObserver(options);

        // Act — fire a 429 (throttle) failure
        FireExceptionEvent(observer, CreateThrottledPayload());

        // Assert — query.failed should fire, but NOT the infra event
        Assert.Single(_logger.GetEntriesByEventName("cosmosdb.query.failed"));
        Assert.Empty(_logger.GetEntriesByEventName("cosmosdb.throttled"));
    }

    [Fact]
    public void InfraEvents_Emitted_WhenEnabled()
    {
        // Arrange — EmitInfrastructureEvents = true
        var observer = CreateObserver(); // uses _infraOptions with EmitInfrastructureEvents = true

        // Act — fire a 429 (throttle) failure
        FireExceptionEvent(observer, CreateThrottledPayload());

        // Assert — BOTH events should fire (supplemental model)
        Assert.Single(_logger.GetEntriesByEventName("cosmosdb.query.failed"));
        Assert.Single(_logger.GetEntriesByEventName("cosmosdb.throttled"));
    }

    [Fact]
    public void NonInfraFailure_StillEmitsQueryFailed_WhenInfraEnabled()
    {
        // Arrange — a 500 status code is a connection error in infra classification
        var observer = CreateObserver();
        var payload = CreateConnectionFailedPayload(statusCode: 500);

        // Act
        FireExceptionEvent(observer, payload);

        // Assert — both query.failed and connection.failed should fire
        Assert.Single(_logger.GetEntriesByEventName("cosmosdb.query.failed"));
        Assert.Single(_logger.GetEntriesByEventName("cosmosdb.connection.failed"));
    }

    // ─── Helper methods ─────────────────────────────────────────────

    /// <summary>
    /// Fires an "Exception" event (failed operation) through the observer.
    /// </summary>
    private static void FireExceptionEvent(OtelEventsCosmosDbObserver observer, object? payload)
    {
        ((IObserver<KeyValuePair<string, object?>>)observer).OnNext(
            new KeyValuePair<string, object?>("Azure.Cosmos.Operation.Exception", payload));
    }

    /// <summary>
    /// Creates an anonymous payload for a connection-level failure
    /// (non-auth, non-throttle — defaults to status 503).
    /// </summary>
    private static object CreateConnectionFailedPayload(
        string database = "TestDb",
        string container = "TestContainer",
        int statusCode = 503,
        double durationMs = 5032.1,
        string errorType = "CosmosException",
        string? endpoint = "https://test.documents.azure.com:443/",
        string failureReason = "ConnectionError",
        Exception? exception = null)
    {
        return new
        {
            DatabaseName = database,
            ContainerName = container,
            RequestCharge = 0.0,
            DurationMs = durationMs,
            StatusCode = statusCode,
            SubStatusCode = 0,
            ErrorType = errorType,
            PartitionKey = (string?)null,
            Exception = exception ?? new InvalidOperationException("Service unavailable"),
            Endpoint = endpoint,
            FailureReason = failureReason,
        };
    }

    /// <summary>
    /// Creates an anonymous payload for an auth failure (HTTP 401 or 403).
    /// </summary>
    private static object CreateAuthFailedPayload(
        string database = "TestDb",
        string container = "TestContainer",
        int statusCode = 401,
        string authScheme = "MasterKey",
        string? identitySource = null,
        Exception? exception = null)
    {
        return new
        {
            DatabaseName = database,
            ContainerName = container,
            RequestCharge = 0.0,
            DurationMs = 12.3,
            StatusCode = statusCode,
            SubStatusCode = 0,
            ErrorType = "CosmosException",
            PartitionKey = (string?)null,
            Exception = exception ?? new InvalidOperationException("Unauthorized"),
            AuthScheme = authScheme,
            IdentitySource = identitySource ?? "default-key",
        };
    }

    /// <summary>
    /// Creates an anonymous payload for a throttled request (HTTP 429).
    /// </summary>
    private static object CreateThrottledPayload(
        string database = "TestDb",
        string container = "TestContainer",
        double requestCharge = 99.9,
        double retryAfterMs = 1234.0,
        Exception? exception = null)
    {
        return new
        {
            DatabaseName = database,
            ContainerName = container,
            RequestCharge = requestCharge,
            DurationMs = 50.0,
            StatusCode = 429,
            SubStatusCode = 3200,
            ErrorType = "CosmosException",
            PartitionKey = (string?)null,
            Exception = exception ?? new InvalidOperationException("Request rate is large"),
            RetryAfterMs = retryAfterMs,
        };
    }
}
