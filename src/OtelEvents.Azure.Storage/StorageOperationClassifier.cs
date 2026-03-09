namespace OtelEvents.Azure.Storage;

/// <summary>
/// Identifies the type of Azure Storage operation detected from the REST API request.
/// </summary>
internal enum StorageOperationType
{
    /// <summary>Blob upload (PUT with blob path).</summary>
    BlobUpload,

    /// <summary>Blob download (GET with blob path).</summary>
    BlobDownload,

    /// <summary>Blob delete (DELETE with blob path).</summary>
    BlobDelete,

    /// <summary>Queue message send (POST to /messages).</summary>
    QueueSend,

    /// <summary>Queue message receive (GET from /messages).</summary>
    QueueReceive
}

/// <summary>
/// Result of classifying an Azure Storage REST API request.
/// Contains the operation type and parsed URI components.
/// </summary>
internal sealed record StorageOperationInfo(
    StorageOperationType Type,
    string AccountName,
    string? ContainerName,
    string? BlobName,
    string? QueueName);

/// <summary>
/// Classifies Azure Storage REST API requests by parsing the URI and HTTP method.
/// Determines whether a request is a blob upload/download/delete or queue send/receive.
/// </summary>
/// <remarks>
/// Azure Storage REST API URI patterns:
/// <list type="bullet">
/// <item>Blob: https://{account}.blob.core.windows.net/{container}/{blob}</item>
/// <item>Queue: https://{account}.queue.core.windows.net/{queue}/messages</item>
/// </list>
/// </remarks>
internal static class StorageOperationClassifier
{
    /// <summary>
    /// Classifies an Azure Storage request based on the URI and HTTP method.
    /// </summary>
    /// <param name="requestUri">The full request URI.</param>
    /// <param name="httpMethod">The HTTP method (GET, PUT, POST, DELETE).</param>
    /// <returns>
    /// A <see cref="StorageOperationInfo"/> if the request is a recognized storage operation;
    /// <c>null</c> if the request is not a storage operation or cannot be classified.
    /// </returns>
    public static StorageOperationInfo? Classify(Uri requestUri, string httpMethod)
    {
        var host = requestUri.Host;

        if (!TryExtractAccountAndService(host, out var accountName, out var serviceType))
        {
            return null;
        }

        var segments = requestUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return serviceType switch
        {
            "BLOB" => ClassifyBlobOperation(accountName, segments, httpMethod),
            "QUEUE" => ClassifyQueueOperation(accountName, segments, httpMethod),
            _ => null
        };
    }

    private static bool TryExtractAccountAndService(
        string host,
        out string accountName,
        out string serviceType)
    {
        accountName = string.Empty;
        serviceType = string.Empty;

        // Expected format: {account}.{service}.core.windows.net
        var parts = host.Split('.');
        if (parts.Length < 5)
        {
            return false;
        }

        if (!string.Equals(parts[^1], "net", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(parts[^2], "windows", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(parts[^3], "core", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        accountName = parts[0];
        serviceType = parts[1].ToUpperInvariant();
        return true;
    }

    private static StorageOperationInfo? ClassifyBlobOperation(
        string accountName,
        string[] segments,
        string httpMethod)
    {
        // Minimum: /{container}/{blob} — need at least 2 path segments
        if (segments.Length < 2)
        {
            return null;
        }

        var containerName = segments[0];
        var blobName = string.Join("/", segments.Skip(1));

        var type = httpMethod.ToUpperInvariant() switch
        {
            "PUT" => StorageOperationType.BlobUpload,
            "GET" => StorageOperationType.BlobDownload,
            "DELETE" => StorageOperationType.BlobDelete,
            _ => (StorageOperationType?)null
        };

        if (type is null)
        {
            return null;
        }

        return new StorageOperationInfo(type.Value, accountName, containerName, blobName, QueueName: null);
    }

    private static StorageOperationInfo? ClassifyQueueOperation(
        string accountName,
        string[] segments,
        string httpMethod)
    {
        // Expected: /{queue}/messages
        if (segments.Length < 2 ||
            !string.Equals(segments[1], "messages", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var queueName = segments[0];

        var type = httpMethod.ToUpperInvariant() switch
        {
            "POST" => StorageOperationType.QueueSend,
            "GET" => StorageOperationType.QueueReceive,
            _ => (StorageOperationType?)null
        };

        if (type is null)
        {
            return null;
        }

        return new StorageOperationInfo(type.Value, accountName, ContainerName: null, BlobName: null, queueName);
    }
}
