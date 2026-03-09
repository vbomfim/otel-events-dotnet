using System.Diagnostics;
using All.Causality;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtelEvents.Grpc.Events;

namespace OtelEvents.Grpc;

/// <summary>
/// gRPC client interceptor that emits schema-defined events for outbound call lifecycle.
/// Emits three events per call: started (10101), completed (10102), failed (10103).
/// Overrides AsyncUnaryCall and AsyncServerStreamingCall for the most common call types.
/// </summary>
/// <remarks>
/// The interceptor observes but never interferes — exceptions are always re-thrown.
/// Service/method exclusion and causal scope creation are configurable
/// via <see cref="OtelEventsGrpcOptions"/>.
/// </remarks>
internal sealed class OtelEventsGrpcClientInterceptor : Interceptor
{
    private const string Side = "Client";

    private readonly ILogger<OtelEventsGrpcEventSource> _logger;
    private readonly OtelEventsGrpcOptions _options;

    public OtelEventsGrpcClientInterceptor(
        ILogger<OtelEventsGrpcEventSource> logger,
        IOptions<OtelEventsGrpcOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>Intercepts async unary client calls.</summary>
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var fullMethod = context.Method.FullName;
        var serviceName = GrpcMethodParser.ExtractServiceName(fullMethod);
        var methodName = GrpcMethodParser.ExtractMethodName(fullMethod);

        if (GrpcMethodParser.IsExcluded(fullMethod, serviceName, _options))
        {
            return continuation(request, context);
        }

        // Emit grpc.call.started (10101)
        _logger.GrpcCallStarted(serviceName, methodName, Side, requestSize: null);

        // Create causal scope
        IDisposable? causalScope = null;
        if (_options.EnableCausalScope)
        {
            causalScope = AllCausalityContext.SetParent(Uuid7.FormatEventId());
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var call = continuation(request, context);

            // Wrap the response task to emit completed/failed on completion
            var wrappedResponseAsync = WrapResponseAsync(
                call.ResponseAsync, serviceName, methodName, sw, causalScope);

            return new AsyncUnaryCall<TResponse>(
                wrappedResponseAsync,
                call.ResponseHeadersAsync,
                call.GetStatus,
                call.GetTrailers,
                call.Dispose);
        }
        catch (RpcException ex)
        {
            sw.Stop();
            EmitFailed(serviceName, methodName, (int)ex.StatusCode, ex.Status.Detail, sw.Elapsed.TotalMilliseconds, ex);
            causalScope?.Dispose();
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            EmitFailed(serviceName, methodName, (int)StatusCode.Internal, null, sw.Elapsed.TotalMilliseconds, ex);
            causalScope?.Dispose();
            throw;
        }
    }

    /// <summary>Intercepts async server streaming client calls.</summary>
    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var fullMethod = context.Method.FullName;
        var serviceName = GrpcMethodParser.ExtractServiceName(fullMethod);
        var methodName = GrpcMethodParser.ExtractMethodName(fullMethod);

        if (GrpcMethodParser.IsExcluded(fullMethod, serviceName, _options))
        {
            return continuation(request, context);
        }

        _logger.GrpcCallStarted(serviceName, methodName, Side, requestSize: null);

        // For streaming calls, emit started only — completed/failed is complex
        // to track on the stream lifecycle. The started event captures intent.
        return continuation(request, context);
    }

    /// <summary>
    /// Wraps the response task to emit completed/failed events on completion.
    /// Disposes the causal scope after the response is received.
    /// </summary>
    private async Task<TResponse> WrapResponseAsync<TResponse>(
        Task<TResponse> responseTask,
        string serviceName,
        string methodName,
        Stopwatch sw,
        IDisposable? causalScope)
    {
        try
        {
            var response = await responseTask;
            sw.Stop();

            _logger.GrpcCallCompleted(
                serviceName, methodName, Side,
                grpcStatusCode: (int)StatusCode.OK,
                grpcStatusDetail: null,
                durationMs: sw.Elapsed.TotalMilliseconds,
                requestSize: null, responseSize: null);

            return response;
        }
        catch (RpcException ex)
        {
            sw.Stop();
            EmitFailed(serviceName, methodName, (int)ex.StatusCode, ex.Status.Detail, sw.Elapsed.TotalMilliseconds, ex);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            EmitFailed(serviceName, methodName, (int)StatusCode.Internal, null, sw.Elapsed.TotalMilliseconds, ex);
            throw;
        }
        finally
        {
            causalScope?.Dispose();
        }
    }

    /// <summary>
    /// Emits the grpc.call.failed event (10103). Shared by all handler types.
    /// </summary>
    private void EmitFailed(
        string serviceName,
        string methodName,
        int statusCode,
        string? statusDetail,
        double durationMs,
        Exception exception)
    {
        _logger.GrpcCallFailed(
            serviceName, methodName, Side,
            grpcStatusCode: statusCode,
            grpcStatusDetail: statusDetail,
            durationMs: durationMs,
            errorType: exception.GetType().Name,
            exception: exception);
    }
}
