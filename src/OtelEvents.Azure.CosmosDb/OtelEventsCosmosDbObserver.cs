using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OtelEvents.Azure.CosmosDb.Events;

namespace OtelEvents.Azure.CosmosDb;

/// <summary>
/// Subscribes to Azure CosmosDB SDK diagnostic events and emits
/// schema-defined structured log events.
/// </summary>
/// <remarks>
/// <para>
/// Implements the dual-observer pattern for <see cref="DiagnosticListener"/>:
/// </para>
/// <list type="bullet">
///   <item><see cref="IObserver{DiagnosticListener}"/> — discovers the
///   <c>Azure.Cosmos.Operation</c> diagnostic source.</item>
///   <item><see cref="IObserver{T}"/> where T is <c>KeyValuePair&lt;string, object?&gt;</c>
///   — processes individual operation events.</item>
/// </list>
/// <para>
/// The observer reads operation details from anonymous payload objects via reflection,
/// so it does not require a direct reference to the Microsoft.Azure.Cosmos package.
/// </para>
/// </remarks>
public sealed class OtelEventsCosmosDbObserver :
    IObserver<DiagnosticListener>,
    IObserver<KeyValuePair<string, object?>>,
    IDisposable
{
    /// <summary>
    /// The diagnostic source name emitted by the Azure CosmosDB .NET SDK v3.
    /// </summary>
    internal const string DiagnosticSourceName = "Azure.Cosmos.Operation";

    private readonly ILogger<OtelEventsCosmosDbEventSource> _logger;
    private readonly OtelEventsCosmosDbOptions _options;
    private IDisposable? _allListenersSubscription;
    private IDisposable? _cosmosListenerSubscription;

    /// <summary>
    /// Cache for reflected property lookups — avoids repeated GetProperty calls
    /// on hot-path diagnostic event processing.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> s_propertyCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OtelEventsCosmosDbObserver"/> class.
    /// </summary>
    /// <param name="logger">Logger for emitting structured CosmosDB operation events.</param>
    /// <param name="options">Configuration options controlling event emission.</param>
    internal OtelEventsCosmosDbObserver(
        ILogger<OtelEventsCosmosDbEventSource> logger,
        OtelEventsCosmosDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options;
    }

    /// <summary>
    /// Subscribes to <see cref="DiagnosticListener.AllListeners"/> to discover
    /// the CosmosDB diagnostic source. Called during DI registration.
    /// </summary>
    internal void Subscribe()
    {
        _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);
    }

    // ─── IObserver<DiagnosticListener> ──────────────────────────────────

    /// <summary>
    /// Called when a new <see cref="DiagnosticListener"/> is created.
    /// Subscribes to the CosmosDB diagnostic source when discovered.
    /// </summary>
    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == DiagnosticSourceName)
        {
            _cosmosListenerSubscription?.Dispose();
            _cosmosListenerSubscription = listener.Subscribe(this);
        }
    }

    /// <inheritdoc/>
    public void OnError(Exception error)
    {
        // No-op per IObserver contract — diagnostic listener errors are non-fatal
    }

    /// <inheritdoc/>
    public void OnCompleted()
    {
        // No-op per IObserver contract — diagnostic listeners complete on shutdown
    }

    // ─── IObserver<KeyValuePair<string, object?>> ───────────────────────

    /// <summary>
    /// Processes individual diagnostic events from the CosmosDB SDK.
    /// </summary>
    void IObserver<KeyValuePair<string, object?>>.OnNext(KeyValuePair<string, object?> pair)
    {
        try
        {
            switch (pair.Key)
            {
                case "Azure.Cosmos.Operation.Stop":
                    ProcessOperationCompleted(pair.Value);
                    break;
                case "Azure.Cosmos.Operation.Exception":
                    ProcessOperationFailed(pair.Value);
                    break;
            }
        }
        catch
        {
            // Observability must never throw — silently swallow exceptions.
            // A telemetry failure should never affect application behavior.
        }
    }

    /// <inheritdoc/>
    void IObserver<KeyValuePair<string, object?>>.OnError(Exception error)
    {
        // No-op — diagnostic event errors are non-fatal
    }

    /// <inheritdoc/>
    void IObserver<KeyValuePair<string, object?>>.OnCompleted()
    {
        // No-op — diagnostic event stream completion is expected on shutdown
    }

    // ─── Event processing ───────────────────────────────────────────────

    /// <summary>
    /// Processes a successful CosmosDB operation completion event.
    /// Determines the operation type and emits the appropriate event.
    /// </summary>
    private void ProcessOperationCompleted(object? payload)
    {
        if (payload is null)
        {
            return;
        }

        var database = ReadProperty<string>(payload, "DatabaseName") ?? "unknown";
        var container = ReadProperty<string>(payload, "ContainerName") ?? "unknown";
        var statusCode = ReadProperty<int>(payload, "StatusCode");
        var requestCharge = ReadProperty<double>(payload, "RequestCharge");
        var durationMs = ReadProperty<double>(payload, "DurationMs");
        var operationType = ReadProperty<string>(payload, "OperationType") ?? "Unknown";
        var itemCount = ReadProperty<int>(payload, "ItemCount");
        var region = _options.CaptureRegion ? ReadProperty<string>(payload, "Region") : null;
        var partitionKey = ReadProperty<string>(payload, "PartitionKey");
        var queryText = ReadProperty<string>(payload, "QueryText");

        // Apply thresholds — skip events below configured limits
        if (_options.RuThreshold > 0 && requestCharge < _options.RuThreshold)
        {
            return;
        }

        if (_options.LatencyThresholdMs > 0 && durationMs < _options.LatencyThresholdMs)
        {
            return;
        }

        // Route to the appropriate event based on operation type
        switch (operationType)
        {
            case "Query":
            case "ReadFeed":
                EmitQueryExecuted(
                    database, container, requestCharge, itemCount,
                    durationMs, statusCode, region, partitionKey, queryText);
                break;

            case "ReadItem":
            case "PointRead":
                EmitPointRead(
                    database, container, partitionKey ?? string.Empty,
                    requestCharge, durationMs, statusCode, region);
                break;

            case "CreateItem":
            case "UpsertItem":
            case "ReplaceItem":
            case "PointWrite":
                EmitPointWrite(
                    database, container, partitionKey ?? string.Empty,
                    requestCharge, durationMs, statusCode, region);
                break;

            default:
                // Unknown operation types emitted as queries (safe fallback)
                EmitQueryExecuted(
                    database, container, requestCharge, itemCount,
                    durationMs, statusCode, region, partitionKey, queryText);
                break;
        }
    }

    /// <summary>
    /// Processes a failed CosmosDB operation event.
    /// All failures emit <c>cosmosdb.query.failed</c> regardless of operation type.
    /// </summary>
    private void ProcessOperationFailed(object? payload)
    {
        if (payload is null)
        {
            return;
        }

        var database = ReadProperty<string>(payload, "DatabaseName") ?? "unknown";
        var container = ReadProperty<string>(payload, "ContainerName") ?? "unknown";
        var statusCode = ReadProperty<int>(payload, "StatusCode");
        var subStatusCode = ReadPropertyNullable<int>(payload, "SubStatusCode");
        var requestCharge = ReadProperty<double>(payload, "RequestCharge");
        var durationMs = ReadProperty<double>(payload, "DurationMs");
        var errorType = ReadProperty<string>(payload, "ErrorType") ?? "Unknown";
        var partitionKey = ReadProperty<string>(payload, "PartitionKey");
        var exception = ReadProperty<Exception>(payload, "Exception");

        _logger.CosmosDbQueryFailed(
            cosmosDatabase: database,
            cosmosContainer: container,
            cosmosRequestCharge: requestCharge,
            durationMs: durationMs,
            cosmosStatusCode: statusCode,
            cosmosSubStatusCode: subStatusCode,
            errorType: errorType,
            cosmosPartitionKey: partitionKey,
            exception: exception);
    }

    // ─── Event emission helpers ─────────────────────────────────────────

    private void EmitQueryExecuted(
        string database,
        string container,
        double requestCharge,
        int itemCount,
        double durationMs,
        int statusCode,
        string? region,
        string? partitionKey,
        string? queryText)
    {
        var sanitizedQuery = _options.CaptureQueryText && queryText is not null
            ? CosmosQuerySanitizer.Sanitize(queryText)
            : null;

        _logger.CosmosDbQueryExecuted(
            cosmosDatabase: database,
            cosmosContainer: container,
            cosmosRequestCharge: requestCharge,
            cosmosItemCount: itemCount,
            durationMs: durationMs,
            cosmosStatusCode: statusCode,
            cosmosRegion: region,
            cosmosPartitionKey: partitionKey,
            cosmosQueryText: sanitizedQuery);
    }

    private void EmitPointRead(
        string database,
        string container,
        string partitionKey,
        double requestCharge,
        double durationMs,
        int statusCode,
        string? region)
    {
        _logger.CosmosDbPointRead(
            cosmosDatabase: database,
            cosmosContainer: container,
            cosmosPartitionKey: partitionKey,
            cosmosRequestCharge: requestCharge,
            durationMs: durationMs,
            cosmosStatusCode: statusCode,
            cosmosRegion: region);
    }

    private void EmitPointWrite(
        string database,
        string container,
        string partitionKey,
        double requestCharge,
        double durationMs,
        int statusCode,
        string? region)
    {
        _logger.CosmosDbPointWrite(
            cosmosDatabase: database,
            cosmosContainer: container,
            cosmosPartitionKey: partitionKey,
            cosmosRequestCharge: requestCharge,
            durationMs: durationMs,
            cosmosStatusCode: statusCode,
            cosmosRegion: region);
    }

    // ─── Reflection helpers ─────────────────────────────────────────────

    /// <summary>
    /// Reads a property value from an anonymous payload object using cached reflection.
    /// Returns <c>default</c> if the property doesn't exist or the value cannot be converted.
    /// </summary>
    private static T? ReadProperty<T>(object obj, string propertyName)
    {
        var type = obj.GetType();
        var key = (type, propertyName);

        var property = s_propertyCache.GetOrAdd(key, static k => k.Item1.GetProperty(k.Item2));
        if (property is null)
        {
            return default;
        }

        var value = property.GetValue(obj);
        if (value is T typed)
        {
            return typed;
        }

        if (value is null)
        {
            return default;
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Reads a nullable value type property from an anonymous payload object.
    /// Returns the value as nullable, or <c>null</c> if the property doesn't exist.
    /// </summary>
    private static T? ReadPropertyNullable<T>(object obj, string propertyName) where T : struct
    {
        var type = obj.GetType();
        var key = (type, propertyName);

        var property = s_propertyCache.GetOrAdd(key, static k => k.Item1.GetProperty(k.Item2));
        if (property is null)
        {
            return null;
        }

        var value = property.GetValue(obj);
        if (value is T typed)
        {
            return typed;
        }

        if (value is null)
        {
            return null;
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cosmosListenerSubscription?.Dispose();
        _allListenersSubscription?.Dispose();
    }
}
