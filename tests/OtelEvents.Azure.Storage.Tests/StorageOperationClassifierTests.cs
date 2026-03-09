namespace OtelEvents.Azure.Storage.Tests;

/// <summary>
/// Tests for <see cref="StorageOperationClassifier"/> — URI parsing logic.
/// Verifies correct classification of Azure Storage REST API requests.
/// </summary>
public sealed class StorageOperationClassifierTests
{
    // ─── Blob Upload (PUT) ─────────────────────────────────────────────

    [Fact]
    public void Classify_BlobUpload_ReturnsCorrectInfo()
    {
        var uri = new Uri("https://myaccount.blob.core.windows.net/mycontainer/myblob.txt");

        var result = StorageOperationClassifier.Classify(uri, "PUT");

        Assert.NotNull(result);
        Assert.Equal(StorageOperationType.BlobUpload, result.Type);
        Assert.Equal("myaccount", result.AccountName);
        Assert.Equal("mycontainer", result.ContainerName);
        Assert.Equal("myblob.txt", result.BlobName);
        Assert.Null(result.QueueName);
    }

    [Fact]
    public void Classify_BlobUpload_NestedPath_ExtractsFullBlobPath()
    {
        var uri = new Uri("https://myaccount.blob.core.windows.net/container/path/to/deep/blob.json");

        var result = StorageOperationClassifier.Classify(uri, "PUT");

        Assert.NotNull(result);
        Assert.Equal(StorageOperationType.BlobUpload, result.Type);
        Assert.Equal("container", result.ContainerName);
        Assert.Equal("path/to/deep/blob.json", result.BlobName);
    }

    [Fact]
    public void Classify_BlobUpload_WithQueryParams_StillClassifies()
    {
        var uri = new Uri("https://myaccount.blob.core.windows.net/container/blob?comp=block&blockid=abc");

        var result = StorageOperationClassifier.Classify(uri, "PUT");

        Assert.NotNull(result);
        Assert.Equal(StorageOperationType.BlobUpload, result.Type);
    }

    // ─── Blob Download (GET) ───────────────────────────────────────────

    [Fact]
    public void Classify_BlobDownload_ReturnsCorrectInfo()
    {
        var uri = new Uri("https://myaccount.blob.core.windows.net/data/report.csv");

        var result = StorageOperationClassifier.Classify(uri, "GET");

        Assert.NotNull(result);
        Assert.Equal(StorageOperationType.BlobDownload, result.Type);
        Assert.Equal("myaccount", result.AccountName);
        Assert.Equal("data", result.ContainerName);
        Assert.Equal("report.csv", result.BlobName);
    }

    // ─── Blob Delete (DELETE) ──────────────────────────────────────────

    [Fact]
    public void Classify_BlobDelete_ReturnsCorrectInfo()
    {
        var uri = new Uri("https://myaccount.blob.core.windows.net/archive/old-file.zip");

        var result = StorageOperationClassifier.Classify(uri, "DELETE");

        Assert.NotNull(result);
        Assert.Equal(StorageOperationType.BlobDelete, result.Type);
        Assert.Equal("myaccount", result.AccountName);
        Assert.Equal("archive", result.ContainerName);
        Assert.Equal("old-file.zip", result.BlobName);
    }

    // ─── Queue Send (POST) ────────────────────────────────────────────

    [Fact]
    public void Classify_QueueSend_ReturnsCorrectInfo()
    {
        var uri = new Uri("https://myaccount.queue.core.windows.net/myqueue/messages");

        var result = StorageOperationClassifier.Classify(uri, "POST");

        Assert.NotNull(result);
        Assert.Equal(StorageOperationType.QueueSend, result.Type);
        Assert.Equal("myaccount", result.AccountName);
        Assert.Equal("myqueue", result.QueueName);
        Assert.Null(result.ContainerName);
        Assert.Null(result.BlobName);
    }

    // ─── Queue Receive (GET) ──────────────────────────────────────────

    [Fact]
    public void Classify_QueueReceive_ReturnsCorrectInfo()
    {
        var uri = new Uri("https://myaccount.queue.core.windows.net/tasks/messages?numofmessages=10");

        var result = StorageOperationClassifier.Classify(uri, "GET");

        Assert.NotNull(result);
        Assert.Equal(StorageOperationType.QueueReceive, result.Type);
        Assert.Equal("myaccount", result.AccountName);
        Assert.Equal("tasks", result.QueueName);
    }

    // ─── Edge Cases ───────────────────────────────────────────────────

    [Fact]
    public void Classify_NonStorageHost_ReturnsNull()
    {
        var uri = new Uri("https://example.com/container/blob");

        var result = StorageOperationClassifier.Classify(uri, "PUT");

        Assert.Null(result);
    }

    [Fact]
    public void Classify_BlobContainerOnly_NoBlob_ReturnsNull()
    {
        // Container-level operation (e.g., list blobs) — not a blob operation
        var uri = new Uri("https://myaccount.blob.core.windows.net/mycontainer?restype=container&comp=list");

        var result = StorageOperationClassifier.Classify(uri, "GET");

        Assert.Null(result);
    }

    [Fact]
    public void Classify_QueueWithoutMessages_ReturnsNull()
    {
        // Queue-level operation (e.g., create queue) — not a message operation
        var uri = new Uri("https://myaccount.queue.core.windows.net/myqueue");

        var result = StorageOperationClassifier.Classify(uri, "PUT");

        Assert.Null(result);
    }

    [Fact]
    public void Classify_BlobUnsupportedMethod_ReturnsNull()
    {
        var uri = new Uri("https://myaccount.blob.core.windows.net/container/blob");

        var result = StorageOperationClassifier.Classify(uri, "HEAD");

        Assert.Null(result);
    }

    [Fact]
    public void Classify_QueueUnsupportedMethod_ReturnsNull()
    {
        var uri = new Uri("https://myaccount.queue.core.windows.net/queue/messages");

        var result = StorageOperationClassifier.Classify(uri, "DELETE");

        Assert.Null(result);
    }

    [Fact]
    public void Classify_UnknownService_ReturnsNull()
    {
        // Table storage — not handled by this pack
        var uri = new Uri("https://myaccount.table.core.windows.net/mytable");

        var result = StorageOperationClassifier.Classify(uri, "POST");

        Assert.Null(result);
    }

    [Fact]
    public void Classify_RootBlobPath_ReturnsNull()
    {
        // Root-level request (e.g., list containers)
        var uri = new Uri("https://myaccount.blob.core.windows.net/");

        var result = StorageOperationClassifier.Classify(uri, "GET");

        Assert.Null(result);
    }

    [Fact]
    public void Classify_AccountName_ExtractedCorrectly()
    {
        var uri = new Uri("https://prodstorageaccount01.blob.core.windows.net/logs/app.log");

        var result = StorageOperationClassifier.Classify(uri, "GET");

        Assert.NotNull(result);
        Assert.Equal("prodstorageaccount01", result.AccountName);
    }
}
