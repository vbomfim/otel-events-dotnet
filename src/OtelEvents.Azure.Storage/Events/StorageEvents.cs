using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace OtelEvents.Azure.Storage.Events;

/// <summary>
/// Pre-compiled [LoggerMessage] methods and metrics for Azure Storage events.
/// Maps to the azure-storage.all.yaml schema (event IDs 10301–10307).
/// </summary>
/// <remarks>
/// This code is pre-compiled in the NuGet package — consumers do NOT need
/// All.Schema at build time. The YAML schema is embedded for documentation
/// and tooling inspection only.
/// </remarks>
internal static partial class StorageEvents
{
    // ─── Meter & Instruments ────────────────────────────────────────────

    private static readonly Meter s_meter = new("OtelEvents.Azure.Storage", "1.0.0");

    /// <summary>Histogram: blob upload duration in ms.</summary>
    internal static readonly Histogram<double> BlobUploadDuration =
        s_meter.CreateHistogram<double>(
            "otel.storage.blob.upload.duration", "ms", "Blob upload duration");

    /// <summary>Histogram: blob upload size in bytes.</summary>
    internal static readonly Histogram<double> BlobUploadSize =
        s_meter.CreateHistogram<double>(
            "otel.storage.blob.upload.size", "bytes", "Blob upload size");

    /// <summary>Counter: total blob uploads.</summary>
    internal static readonly Counter<long> BlobUploadCount =
        s_meter.CreateCounter<long>(
            "otel.storage.blob.upload.count", "uploads", "Total blob uploads");

    /// <summary>Histogram: blob download duration in ms.</summary>
    internal static readonly Histogram<double> BlobDownloadDuration =
        s_meter.CreateHistogram<double>(
            "otel.storage.blob.download.duration", "ms", "Blob download duration");

    /// <summary>Histogram: blob download size in bytes.</summary>
    internal static readonly Histogram<double> BlobDownloadSize =
        s_meter.CreateHistogram<double>(
            "otel.storage.blob.download.size", "bytes", "Blob download size");

    /// <summary>Counter: total blob deletes.</summary>
    internal static readonly Counter<long> BlobDeleteCount =
        s_meter.CreateCounter<long>(
            "otel.storage.blob.delete.count", "deletes", "Total blob deletes");

    /// <summary>Counter: total blob operation errors.</summary>
    internal static readonly Counter<long> BlobErrorCount =
        s_meter.CreateCounter<long>(
            "otel.storage.blob.error.count", "errors", "Total blob operation errors");

    /// <summary>Counter: total messages sent to queue.</summary>
    internal static readonly Counter<long> QueueSendCount =
        s_meter.CreateCounter<long>(
            "otel.storage.queue.send.count", "messages", "Total messages sent to queue");

    /// <summary>Histogram: queue send duration in ms.</summary>
    internal static readonly Histogram<double> QueueSendDuration =
        s_meter.CreateHistogram<double>(
            "otel.storage.queue.send.duration", "ms", "Queue send duration");

    /// <summary>Counter: total queue receive operations.</summary>
    internal static readonly Counter<long> QueueReceiveCount =
        s_meter.CreateCounter<long>(
            "otel.storage.queue.receive.count", "receives", "Total queue receive operations");

    /// <summary>Histogram: messages received per batch.</summary>
    internal static readonly Histogram<double> QueueReceiveMessageCount =
        s_meter.CreateHistogram<double>(
            "otel.storage.queue.receive.message.count", "messages", "Messages received per batch");

    /// <summary>Counter: total queue operation errors.</summary>
    internal static readonly Counter<long> QueueErrorCount =
        s_meter.CreateCounter<long>(
            "otel.storage.queue.error.count", "errors", "Total queue operation errors");

    // ─── Event: storage.blob.uploaded (ID 10301) ────────────────────────

    [LoggerMessage(
        EventId = 10301,
        EventName = "storage.blob.uploaded",
        Level = LogLevel.Information,
        Message = "Blob uploaded to {storageContainerName}/{storageBlobName} ({storageBlobSize} bytes) in {durationMs}ms")]
    private static partial void LogStorageBlobUploaded(
        ILogger logger,
        string storageAccountName,
        string storageContainerName,
        string storageBlobName,
        long storageBlobSize,
        string? storageContentType,
        double durationMs,
        int storageStatusCode);

    /// <summary>
    /// Emits the <c>storage.blob.uploaded</c> event (ID 10301) and records metrics.
    /// </summary>
    internal static void StorageBlobUploaded(
        this ILogger logger,
        string storageAccountName,
        string storageContainerName,
        string storageBlobName,
        long storageBlobSize,
        string? storageContentType,
        double durationMs,
        int storageStatusCode)
    {
        LogStorageBlobUploaded(logger, storageAccountName, storageContainerName,
            storageBlobName, storageBlobSize, storageContentType, durationMs, storageStatusCode);

        var containerTag = new KeyValuePair<string, object?>("storageContainerName", storageContainerName);
        BlobUploadDuration.Record(durationMs, containerTag);
        BlobUploadSize.Record(storageBlobSize, containerTag);
        BlobUploadCount.Add(1, containerTag);
    }

    // ─── Event: storage.blob.downloaded (ID 10302) ──────────────────────

    [LoggerMessage(
        EventId = 10302,
        EventName = "storage.blob.downloaded",
        Level = LogLevel.Information,
        Message = "Blob downloaded from {storageContainerName}/{storageBlobName} ({storageBlobSize} bytes) in {durationMs}ms")]
    private static partial void LogStorageBlobDownloaded(
        ILogger logger,
        string storageAccountName,
        string storageContainerName,
        string storageBlobName,
        long storageBlobSize,
        string? storageContentType,
        double durationMs,
        int storageStatusCode);

    /// <summary>
    /// Emits the <c>storage.blob.downloaded</c> event (ID 10302) and records metrics.
    /// </summary>
    internal static void StorageBlobDownloaded(
        this ILogger logger,
        string storageAccountName,
        string storageContainerName,
        string storageBlobName,
        long storageBlobSize,
        string? storageContentType,
        double durationMs,
        int storageStatusCode)
    {
        LogStorageBlobDownloaded(logger, storageAccountName, storageContainerName,
            storageBlobName, storageBlobSize, storageContentType, durationMs, storageStatusCode);

        var containerTag = new KeyValuePair<string, object?>("storageContainerName", storageContainerName);
        BlobDownloadDuration.Record(durationMs, containerTag);
        BlobDownloadSize.Record(storageBlobSize, containerTag);
    }

    // ─── Event: storage.blob.deleted (ID 10303) ─────────────────────────

    [LoggerMessage(
        EventId = 10303,
        EventName = "storage.blob.deleted",
        Level = LogLevel.Information,
        Message = "Blob deleted from {storageContainerName}/{storageBlobName} in {durationMs}ms")]
    private static partial void LogStorageBlobDeleted(
        ILogger logger,
        string storageAccountName,
        string storageContainerName,
        string storageBlobName,
        double durationMs,
        int storageStatusCode);

    /// <summary>
    /// Emits the <c>storage.blob.deleted</c> event (ID 10303) and records metrics.
    /// </summary>
    internal static void StorageBlobDeleted(
        this ILogger logger,
        string storageAccountName,
        string storageContainerName,
        string storageBlobName,
        double durationMs,
        int storageStatusCode)
    {
        LogStorageBlobDeleted(logger, storageAccountName, storageContainerName,
            storageBlobName, durationMs, storageStatusCode);

        BlobDeleteCount.Add(1,
            new KeyValuePair<string, object?>("storageContainerName", storageContainerName));
    }

    // ─── Event: storage.blob.failed (ID 10304) ──────────────────────────

    [LoggerMessage(
        EventId = 10304,
        EventName = "storage.blob.failed",
        Level = LogLevel.Error,
        Message = "Blob operation on {storageContainerName}/{storageBlobName} failed with {storageStatusCode} after {durationMs}ms")]
    private static partial void LogStorageBlobFailed(
        ILogger logger,
        Exception? exception,
        string storageAccountName,
        string storageContainerName,
        string? storageBlobName,
        double durationMs,
        int storageStatusCode,
        string errorType);

    /// <summary>
    /// Emits the <c>storage.blob.failed</c> event (ID 10304) and records metrics.
    /// </summary>
    internal static void StorageBlobFailed(
        this ILogger logger,
        string storageAccountName,
        string storageContainerName,
        string? storageBlobName,
        double durationMs,
        int storageStatusCode,
        string errorType,
        Exception? exception = null)
    {
        LogStorageBlobFailed(logger, exception, storageAccountName, storageContainerName,
            storageBlobName, durationMs, storageStatusCode, errorType);

        BlobErrorCount.Add(1,
            new KeyValuePair<string, object?>("storageContainerName", storageContainerName),
            new KeyValuePair<string, object?>("storageStatusCode", storageStatusCode));
    }

    // ─── Event: storage.queue.sent (ID 10305) ───────────────────────────

    [LoggerMessage(
        EventId = 10305,
        EventName = "storage.queue.sent",
        Level = LogLevel.Information,
        Message = "Message sent to queue {storageQueueName} in {durationMs}ms")]
    private static partial void LogStorageQueueSent(
        ILogger logger,
        string storageAccountName,
        string storageQueueName,
        double durationMs,
        int storageStatusCode);

    /// <summary>
    /// Emits the <c>storage.queue.sent</c> event (ID 10305) and records metrics.
    /// </summary>
    internal static void StorageQueueSent(
        this ILogger logger,
        string storageAccountName,
        string storageQueueName,
        double durationMs,
        int storageStatusCode)
    {
        LogStorageQueueSent(logger, storageAccountName, storageQueueName, durationMs, storageStatusCode);

        var queueTag = new KeyValuePair<string, object?>("storageQueueName", storageQueueName);
        QueueSendCount.Add(1, queueTag);
        QueueSendDuration.Record(durationMs, queueTag);
    }

    // ─── Event: storage.queue.received (ID 10306) ───────────────────────

    [LoggerMessage(
        EventId = 10306,
        EventName = "storage.queue.received",
        Level = LogLevel.Information,
        Message = "Received {storageMessageCount} messages from queue {storageQueueName} in {durationMs}ms")]
    private static partial void LogStorageQueueReceived(
        ILogger logger,
        string storageAccountName,
        string storageQueueName,
        int storageMessageCount,
        double durationMs,
        int storageStatusCode);

    /// <summary>
    /// Emits the <c>storage.queue.received</c> event (ID 10306) and records metrics.
    /// </summary>
    internal static void StorageQueueReceived(
        this ILogger logger,
        string storageAccountName,
        string storageQueueName,
        int storageMessageCount,
        double durationMs,
        int storageStatusCode)
    {
        LogStorageQueueReceived(logger, storageAccountName, storageQueueName,
            storageMessageCount, durationMs, storageStatusCode);

        var queueTag = new KeyValuePair<string, object?>("storageQueueName", storageQueueName);
        QueueReceiveCount.Add(1, queueTag);
        QueueReceiveMessageCount.Record(storageMessageCount, queueTag);
    }

    // ─── Event: storage.queue.failed (ID 10307) ─────────────────────────

    [LoggerMessage(
        EventId = 10307,
        EventName = "storage.queue.failed",
        Level = LogLevel.Error,
        Message = "Queue operation on {storageQueueName} failed with {storageStatusCode} after {durationMs}ms")]
    private static partial void LogStorageQueueFailed(
        ILogger logger,
        Exception? exception,
        string storageAccountName,
        string? storageQueueName,
        double durationMs,
        int storageStatusCode,
        string errorType);

    /// <summary>
    /// Emits the <c>storage.queue.failed</c> event (ID 10307) and records metrics.
    /// </summary>
    internal static void StorageQueueFailed(
        this ILogger logger,
        string storageAccountName,
        string? storageQueueName,
        double durationMs,
        int storageStatusCode,
        string errorType,
        Exception? exception = null)
    {
        LogStorageQueueFailed(logger, exception, storageAccountName, storageQueueName,
            durationMs, storageStatusCode, errorType);

        QueueErrorCount.Add(1,
            new KeyValuePair<string, object?>("storageQueueName", storageQueueName),
            new KeyValuePair<string, object?>("storageStatusCode", storageStatusCode));
    }
}
