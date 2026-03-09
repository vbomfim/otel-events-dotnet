using System.Diagnostics;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Extensions.Logging;
using OtelEvents.Azure.Storage.Events;

namespace OtelEvents.Azure.Storage;

/// <summary>
/// Azure SDK pipeline policy that observes Blob and Queue operations
/// and emits schema-defined structured events with metrics.
/// </summary>
/// <remarks>
/// <para>
/// This policy intercepts Azure Storage REST API requests passing through the
/// Azure SDK HTTP pipeline. It classifies each request by URI and HTTP method,
/// measures operation duration, and emits the appropriate schema-defined event
/// (IDs 10301–10307).
/// </para>
/// <para>
/// The policy observes but never interferes — exceptions are always re-thrown,
/// and failed event emission never blocks the pipeline.
/// </para>
/// </remarks>
internal sealed class OtelEventsStoragePipelinePolicy : HttpPipelinePolicy
{
    private readonly ILogger<OtelEventsStorageEventSource> _logger;
    private readonly OtelEventsAzureStorageOptions _options;

    public OtelEventsStoragePipelinePolicy(
        ILogger<OtelEventsStorageEventSource> logger,
        OtelEventsAzureStorageOptions options)
    {
        _logger = logger;
        _options = options;
    }

    /// <summary>
    /// Processes the HTTP message synchronously, emitting events after completion.
    /// </summary>
    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            ProcessNext(message, pipeline);
            sw.Stop();
            EmitEvent(message, sw.Elapsed.TotalMilliseconds, exception: null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            EmitEvent(message, sw.Elapsed.TotalMilliseconds, exception: ex);
            throw; // Re-throw — observe, never swallow
        }
    }

    /// <summary>
    /// Processes the HTTP message asynchronously, emitting events after completion.
    /// </summary>
    public override async ValueTask ProcessAsync(
        HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await ProcessNextAsync(message, pipeline);
            sw.Stop();
            EmitEvent(message, sw.Elapsed.TotalMilliseconds, exception: null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            EmitEvent(message, sw.Elapsed.TotalMilliseconds, exception: ex);
            throw; // Re-throw — observe, never swallow
        }
    }

    /// <summary>
    /// Classifies the request and emits the appropriate event with metrics.
    /// </summary>
    private void EmitEvent(HttpMessage message, double durationMs, Exception? exception)
    {
        var requestUri = message.Request.Uri.ToUri();
        var httpMethod = message.Request.Method.Method;
        var operation = StorageOperationClassifier.Classify(requestUri, httpMethod);

        if (operation is null)
        {
            return;
        }

        if (!IsOperationEnabled(operation))
        {
            return;
        }

        if (IsExcluded(operation))
        {
            return;
        }

        var statusCode = message.Response?.Status ?? 0;
        var contentLength = GetContentLength(message);
        var isError = exception is not null || statusCode >= 400;

        if (isError)
        {
            EmitErrorEvent(operation, durationMs, statusCode, exception);
        }
        else
        {
            EmitSuccessEvent(operation, durationMs, statusCode, contentLength);
        }
    }

    /// <summary>
    /// Emits the appropriate success event based on the operation type.
    /// </summary>
    private void EmitSuccessEvent(
        StorageOperationInfo operation,
        double durationMs,
        int statusCode,
        long contentLength)
    {
        switch (operation.Type)
        {
            case StorageOperationType.BlobUpload:
                _logger.StorageBlobUploaded(
                    operation.AccountName,
                    operation.ContainerName!,
                    operation.BlobName!,
                    storageBlobSize: contentLength,
                    storageContentType: null,
                    durationMs,
                    statusCode);
                break;

            case StorageOperationType.BlobDownload:
                _logger.StorageBlobDownloaded(
                    operation.AccountName,
                    operation.ContainerName!,
                    operation.BlobName!,
                    storageBlobSize: contentLength,
                    storageContentType: null,
                    durationMs,
                    statusCode);
                break;

            case StorageOperationType.BlobDelete:
                _logger.StorageBlobDeleted(
                    operation.AccountName,
                    operation.ContainerName!,
                    operation.BlobName!,
                    durationMs,
                    statusCode);
                break;

            case StorageOperationType.QueueSend:
                _logger.StorageQueueSent(
                    operation.AccountName,
                    operation.QueueName!,
                    durationMs,
                    statusCode);
                break;

            case StorageOperationType.QueueReceive:
                _logger.StorageQueueReceived(
                    operation.AccountName,
                    operation.QueueName!,
                    storageMessageCount: 0, // Cannot determine from HTTP response alone
                    durationMs,
                    statusCode);
                break;
        }
    }

    /// <summary>
    /// Emits the appropriate error event based on the operation type.
    /// </summary>
    private void EmitErrorEvent(
        StorageOperationInfo operation,
        double durationMs,
        int statusCode,
        Exception? exception)
    {
        var errorType = exception?.GetType().Name ?? $"Http{statusCode}";

        if (operation.Type is StorageOperationType.QueueSend or StorageOperationType.QueueReceive)
        {
            _logger.StorageQueueFailed(
                operation.AccountName,
                operation.QueueName,
                durationMs,
                statusCode,
                errorType,
                exception);
        }
        else
        {
            _logger.StorageBlobFailed(
                operation.AccountName,
                operation.ContainerName ?? string.Empty,
                operation.BlobName,
                durationMs,
                statusCode,
                errorType,
                exception);
        }
    }

    /// <summary>
    /// Checks whether events for this operation type are enabled in the options.
    /// </summary>
    private bool IsOperationEnabled(StorageOperationInfo operation)
    {
        return operation.Type switch
        {
            StorageOperationType.BlobUpload or
            StorageOperationType.BlobDownload or
            StorageOperationType.BlobDelete => _options.EnableBlobEvents,
            StorageOperationType.QueueSend or
            StorageOperationType.QueueReceive => _options.EnableQueueEvents,
            _ => false
        };
    }

    /// <summary>
    /// Checks whether this operation targets an excluded container or queue.
    /// </summary>
    private bool IsExcluded(StorageOperationInfo operation)
    {
        if (operation.ContainerName is not null && _options.ExcludeContainers.Count > 0)
        {
            for (var i = 0; i < _options.ExcludeContainers.Count; i++)
            {
                if (string.Equals(operation.ContainerName, _options.ExcludeContainers[i],
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        if (operation.QueueName is not null && _options.ExcludeQueues.Count > 0)
        {
            for (var i = 0; i < _options.ExcludeQueues.Count; i++)
            {
                if (string.Equals(operation.QueueName, _options.ExcludeQueues[i],
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts content length from the response headers.
    /// Returns 0 if not available.
    /// </summary>
    private static long GetContentLength(HttpMessage message)
    {
        if (message.Response is null)
        {
            return 0;
        }

        if (message.Response.Headers.TryGetValue("Content-Length", out var value) &&
            long.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var length))
        {
            return length;
        }

        return 0;
    }
}
