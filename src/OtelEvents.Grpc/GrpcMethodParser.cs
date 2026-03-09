namespace OtelEvents.Grpc;

/// <summary>
/// Helper for extracting service name and method name from gRPC method paths.
/// gRPC method paths follow the format: "/package.ServiceName/MethodName".
/// </summary>
internal static class GrpcMethodParser
{
    /// <summary>
    /// Extracts the service name from a gRPC method path.
    /// Input: "/package.ServiceName/MethodName" → Output: "package.ServiceName"
    /// </summary>
    /// <param name="fullMethod">The full gRPC method path (e.g., "/greet.Greeter/SayHello").</param>
    /// <returns>The service name, or "unknown" if the path cannot be parsed.</returns>
    internal static string ExtractServiceName(string? fullMethod)
    {
        if (string.IsNullOrEmpty(fullMethod))
        {
            return "unknown";
        }

        // Strip leading slash
        var path = fullMethod.AsSpan();
        if (path.Length > 0 && path[0] == '/')
        {
            path = path[1..];
        }

        var slashIndex = path.IndexOf('/');
        if (slashIndex <= 0)
        {
            return "unknown";
        }

        return path[..slashIndex].ToString();
    }

    /// <summary>
    /// Extracts the method name from a gRPC method path.
    /// Input: "/package.ServiceName/MethodName" → Output: "MethodName"
    /// </summary>
    /// <param name="fullMethod">The full gRPC method path (e.g., "/greet.Greeter/SayHello").</param>
    /// <returns>The method name, or "unknown" if the path cannot be parsed.</returns>
    internal static string ExtractMethodName(string? fullMethod)
    {
        if (string.IsNullOrEmpty(fullMethod))
        {
            return "unknown";
        }

        // Strip leading slash
        var path = fullMethod.AsSpan();
        if (path.Length > 0 && path[0] == '/')
        {
            path = path[1..];
        }

        var slashIndex = path.IndexOf('/');
        if (slashIndex < 0 || slashIndex == path.Length - 1)
        {
            return "unknown";
        }

        return path[(slashIndex + 1)..].ToString();
    }

    /// <summary>
    /// Determines whether a gRPC method should be excluded based on the options.
    /// </summary>
    internal static bool IsExcluded(string? fullMethod, string serviceName, OtelEventsGrpcOptions options)
    {
        for (var i = 0; i < options.ExcludeServices.Count; i++)
        {
            if (string.Equals(serviceName, options.ExcludeServices[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (fullMethod is not null)
        {
            for (var i = 0; i < options.ExcludeMethods.Count; i++)
            {
                if (string.Equals(fullMethod, options.ExcludeMethods[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
