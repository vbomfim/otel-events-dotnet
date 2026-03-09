using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.Azure.Storage.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsStoragePipelinePolicy"/> — Azure SDK pipeline integration.
/// Verifies that storage operations are observed and events/metrics are emitted correctly.
/// </summary>
public sealed class OtelEventsStoragePipelinePolicyTests
{
    // ─── Test Infrastructure ──────────────────────────────────────────

    private static (HttpPipeline Pipeline, TestLogExporter Exporter) CreateTestPipeline(
        int statusCode = 200,
        long? contentLength = null,
        Action<OtelEventsAzureStorageOptions>? configureOptions = null)
    {
        var exporter = new TestLogExporter();

        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddOpenTelemetry(otel =>
            {
                otel.IncludeFormattedMessage = true;
                otel.ParseStateValues = true;
                otel.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
            });
        });

        var storageOptions = new OtelEventsAzureStorageOptions();
        configureOptions?.Invoke(storageOptions);
        services.AddSingleton(storageOptions);

        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<OtelEvents.Azure.Storage.Events.OtelEventsStorageEventSource>>();

        var policy = new OtelEventsStoragePipelinePolicy(logger, storageOptions);

        var transport = new MockPipelineTransport(statusCode, contentLength);
        var clientOptions = new TestClientOptions { Transport = transport };
        var pipeline = HttpPipelineBuilder.Build(clientOptions, policy);

        return (pipeline, exporter);
    }

    private static HttpMessage CreateMessage(HttpPipeline pipeline, string uri, RequestMethod method)
    {
        var message = pipeline.CreateMessage();
        message.Request.Uri.Reset(new Uri(uri));
        message.Request.Method = method;
        return message;
    }

    // ─── Blob Upload Events ───────────────────────────────────────────

    [Fact]
    public void Process_BlobUpload_EmitsStorageBlobUploaded()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 201, contentLength: 1024);
        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/mycontainer/myblob.txt",
            RequestMethod.Put);

        pipeline.Send(message, CancellationToken.None);

        exporter.AssertEventEmitted("storage.blob.uploaded");
        var record = exporter.AssertSingle("storage.blob.uploaded");
        record.AssertAttribute("storageAccountName", "myaccount");
        record.AssertAttribute("storageContainerName", "mycontainer");
        record.AssertAttribute("storageBlobName", "myblob.txt");
        record.AssertAttribute("storageStatusCode", 201);
        record.AssertHasAttribute("durationMs");
    }

    [Fact]
    public async Task ProcessAsync_BlobUpload_EmitsStorageBlobUploaded()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 201, contentLength: 2048);
        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/data/file.json",
            RequestMethod.Put);

        await pipeline.SendAsync(message, CancellationToken.None);

        exporter.AssertEventEmitted("storage.blob.uploaded");
        var record = exporter.AssertSingle("storage.blob.uploaded");
        record.AssertAttribute("storageAccountName", "myaccount");
        record.AssertAttribute("storageContainerName", "data");
        record.AssertAttribute("storageBlobName", "file.json");
    }

    // ─── Blob Download Events ─────────────────────────────────────────

    [Fact]
    public void Process_BlobDownload_EmitsStorageBlobDownloaded()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 200, contentLength: 4096);
        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/reports/q1-report.pdf",
            RequestMethod.Get);

        pipeline.Send(message, CancellationToken.None);

        exporter.AssertEventEmitted("storage.blob.downloaded");
        var record = exporter.AssertSingle("storage.blob.downloaded");
        record.AssertAttribute("storageAccountName", "myaccount");
        record.AssertAttribute("storageContainerName", "reports");
        record.AssertAttribute("storageBlobName", "q1-report.pdf");
        record.AssertAttribute("storageStatusCode", 200);
        record.AssertAttribute("storageBlobSize", 4096L);
    }

    // ─── Blob Delete Events ───────────────────────────────────────────

    [Fact]
    public void Process_BlobDelete_EmitsStorageBlobDeleted()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 202);
        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/archive/old-backup.zip",
            RequestMethod.Delete);

        pipeline.Send(message, CancellationToken.None);

        exporter.AssertEventEmitted("storage.blob.deleted");
        var record = exporter.AssertSingle("storage.blob.deleted");
        record.AssertAttribute("storageAccountName", "myaccount");
        record.AssertAttribute("storageContainerName", "archive");
        record.AssertAttribute("storageBlobName", "old-backup.zip");
        record.AssertAttribute("storageStatusCode", 202);
    }

    // ─── Queue Send Events ────────────────────────────────────────────

    [Fact]
    public void Process_QueueSend_EmitsStorageQueueSent()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 201);
        var message = CreateMessage(pipeline,
            "https://myaccount.queue.core.windows.net/tasks/messages",
            RequestMethod.Post);

        pipeline.Send(message, CancellationToken.None);

        exporter.AssertEventEmitted("storage.queue.sent");
        var record = exporter.AssertSingle("storage.queue.sent");
        record.AssertAttribute("storageAccountName", "myaccount");
        record.AssertAttribute("storageQueueName", "tasks");
        record.AssertAttribute("storageStatusCode", 201);
    }

    // ─── Queue Receive Events ─────────────────────────────────────────

    [Fact]
    public void Process_QueueReceive_EmitsStorageQueueReceived()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 200);
        var message = CreateMessage(pipeline,
            "https://myaccount.queue.core.windows.net/notifications/messages?numofmessages=10",
            RequestMethod.Get);

        pipeline.Send(message, CancellationToken.None);

        exporter.AssertEventEmitted("storage.queue.received");
        var record = exporter.AssertSingle("storage.queue.received");
        record.AssertAttribute("storageAccountName", "myaccount");
        record.AssertAttribute("storageQueueName", "notifications");
        record.AssertAttribute("storageStatusCode", 200);
    }

    // ─── Error Events (HTTP 4xx/5xx) ──────────────────────────────────

    [Fact]
    public void Process_BlobUpload_4xxResponse_EmitsStorageBlobFailed()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 404);
        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/missing/blob.txt",
            RequestMethod.Put);

        pipeline.Send(message, CancellationToken.None);

        exporter.AssertEventEmitted("storage.blob.failed");
        var record = exporter.AssertSingle("storage.blob.failed");
        record.AssertAttribute("storageAccountName", "myaccount");
        record.AssertAttribute("storageContainerName", "missing");
        record.AssertAttribute("storageStatusCode", 404);
        Assert.Equal(LogLevel.Error, record.LogLevel);
    }

    [Fact]
    public void Process_BlobDownload_5xxResponse_EmitsStorageBlobFailed()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 500);
        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/data/file.csv",
            RequestMethod.Get);

        pipeline.Send(message, CancellationToken.None);

        exporter.AssertEventEmitted("storage.blob.failed");
        var record = exporter.AssertSingle("storage.blob.failed");
        record.AssertAttribute("storageStatusCode", 500);
    }

    [Fact]
    public void Process_QueueSend_ErrorResponse_EmitsStorageQueueFailed()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 503);
        var message = CreateMessage(pipeline,
            "https://myaccount.queue.core.windows.net/tasks/messages",
            RequestMethod.Post);

        pipeline.Send(message, CancellationToken.None);

        exporter.AssertEventEmitted("storage.queue.failed");
        var record = exporter.AssertSingle("storage.queue.failed");
        record.AssertAttribute("storageStatusCode", 503);
        Assert.Equal(LogLevel.Error, record.LogLevel);
    }

    // ─── Exclusion Filters ────────────────────────────────────────────

    [Fact]
    public void Process_ExcludedContainer_NoEventEmitted()
    {
        var (pipeline, exporter) = CreateTestPipeline(
            statusCode: 200,
            configureOptions: o => o.ExcludeContainers = ["logs"]);
        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/logs/app.log",
            RequestMethod.Get);

        pipeline.Send(message, CancellationToken.None);

        exporter.AssertEventNotEmitted("storage.blob.downloaded");
    }

    [Fact]
    public void Process_ExcludedQueue_NoEventEmitted()
    {
        var (pipeline, exporter) = CreateTestPipeline(
            statusCode: 200,
            configureOptions: o => o.ExcludeQueues = ["internal-queue"]);
        var message = CreateMessage(pipeline,
            "https://myaccount.queue.core.windows.net/internal-queue/messages",
            RequestMethod.Post);

        pipeline.Send(message, CancellationToken.None);

        exporter.AssertEventNotEmitted("storage.queue.sent");
    }

    [Fact]
    public void Process_BlobEventsDisabled_NoEventEmitted()
    {
        var (pipeline, exporter) = CreateTestPipeline(
            statusCode: 200,
            configureOptions: o => o.EnableBlobEvents = false);
        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt",
            RequestMethod.Put);

        pipeline.Send(message, CancellationToken.None);

        exporter.AssertEventNotEmitted("storage.blob.uploaded");
    }

    [Fact]
    public void Process_QueueEventsDisabled_NoEventEmitted()
    {
        var (pipeline, exporter) = CreateTestPipeline(
            statusCode: 200,
            configureOptions: o => o.EnableQueueEvents = false);
        var message = CreateMessage(pipeline,
            "https://myaccount.queue.core.windows.net/queue/messages",
            RequestMethod.Post);

        pipeline.Send(message, CancellationToken.None);

        exporter.AssertEventNotEmitted("storage.queue.sent");
    }

    // ─── Non-Storage Requests ─────────────────────────────────────────

    [Fact]
    public void Process_NonStorageUri_NoEventEmitted()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 200);
        var message = CreateMessage(pipeline,
            "https://management.azure.com/subscriptions/abc/resourceGroups",
            RequestMethod.Get);

        pipeline.Send(message, CancellationToken.None);

        Assert.Empty(exporter.LogRecords);
    }

    // ─── Duration Measurement ─────────────────────────────────────────

    [Fact]
    public void Process_BlobUpload_DurationIsPositive()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 201);
        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.bin",
            RequestMethod.Put);

        pipeline.Send(message, CancellationToken.None);

        var record = exporter.AssertSingle("storage.blob.uploaded");
        record.AssertHasAttribute("durationMs");
        var duration = (double)record.Attributes["durationMs"]!;
        Assert.True(duration >= 0, $"Duration should be non-negative, was {duration}");
    }

    // ─── Event ID Correctness ─────────────────────────────────────────

    [Fact]
    public void Process_BlobUpload_HasCorrectEventId()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 201);
        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt",
            RequestMethod.Put);

        pipeline.Send(message, CancellationToken.None);

        var record = exporter.AssertSingle("storage.blob.uploaded");
        Assert.Equal(10301, record.EventId.Id);
    }

    [Fact]
    public void Process_BlobDownload_HasCorrectEventId()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 200, contentLength: 100);
        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt",
            RequestMethod.Get);

        pipeline.Send(message, CancellationToken.None);

        var record = exporter.AssertSingle("storage.blob.downloaded");
        Assert.Equal(10302, record.EventId.Id);
    }

    [Fact]
    public void Process_BlobDelete_HasCorrectEventId()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 202);
        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/container/blob.txt",
            RequestMethod.Delete);

        pipeline.Send(message, CancellationToken.None);

        var record = exporter.AssertSingle("storage.blob.deleted");
        Assert.Equal(10303, record.EventId.Id);
    }

    [Fact]
    public void Process_QueueSend_HasCorrectEventId()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 201);
        var message = CreateMessage(pipeline,
            "https://myaccount.queue.core.windows.net/queue/messages",
            RequestMethod.Post);

        pipeline.Send(message, CancellationToken.None);

        var record = exporter.AssertSingle("storage.queue.sent");
        Assert.Equal(10305, record.EventId.Id);
    }

    [Fact]
    public void Process_QueueReceive_HasCorrectEventId()
    {
        var (pipeline, exporter) = CreateTestPipeline(statusCode: 200);
        var message = CreateMessage(pipeline,
            "https://myaccount.queue.core.windows.net/queue/messages",
            RequestMethod.Get);

        pipeline.Send(message, CancellationToken.None);

        var record = exporter.AssertSingle("storage.queue.received");
        Assert.Equal(10306, record.EventId.Id);
    }

    // ─── Exclusion Case-Insensitivity ─────────────────────────────────

    [Fact]
    public void Process_ExcludedContainer_CaseInsensitive()
    {
        var (pipeline, exporter) = CreateTestPipeline(
            statusCode: 200,
            configureOptions: o => o.ExcludeContainers = ["Logs"]);
        var message = CreateMessage(pipeline,
            "https://myaccount.blob.core.windows.net/logs/app.log",
            RequestMethod.Get);

        pipeline.Send(message, CancellationToken.None);

        exporter.AssertEventNotEmitted("storage.blob.downloaded");
    }
}
