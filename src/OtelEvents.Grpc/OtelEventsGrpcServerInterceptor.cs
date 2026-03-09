using System.Diagnostics;
using OtelEvents.Causality;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtelEvents.Grpc.Events;

namespace OtelEvents.Grpc;

/// <summary>
/// gRPC server interceptor that emits schema-defined events for call lifecycle.
/// Emits three events per call: started (10101), completed (10102), failed (10103).
/// Overrides all four server handler types: Unary, ServerStreaming, ClientStreaming, DuplexStreaming.
/// </summary>
/// <remarks>
/// The interceptor observes but never interferes — exceptions are always re-thrown.
/// Service/method exclusion and causal scope creation are configurable
/// via <see cref="OtelEventsGrpcOptions"/>.
/// </remarks>
internal sealed class OtelEventsGrpcServerInterceptor : Interceptor
{
    private const string Side = "Server";

    private readonly ILogger<OtelEventsGrpcEventSource> _logger;
    private readonly OtelEventsGrpcOptions _options;

    public OtelEventsGrpcServerInterceptor(
        ILogger<OtelEventsGrpcEventSource> logger,
        IOptions<OtelEventsGrpcOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>Intercepts unary server calls.</summary>
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var fullMethod = context.Method;
        var serviceName = GrpcMethodParser.ExtractServiceName(fullMethod);
        var methodName = GrpcMethodParser.ExtractMethodName(fullMethod);

        if (GrpcMethodParser.IsExcluded(fullMethod, serviceName, _options))
        {
            return await continuation(request, context);
        }

        // Emit grpc.call.started (10101)
        _logger.GrpcCallStarted(serviceName, methodName, Side, requestSize: null);

        // Create causal scope
        IDisposable? causalScope = null;
        if (_options.EnableCausalScope)
        {
            causalScope = OtelEventsCausalityContext.SetParent(Uuid7.FormatEventId());
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await continuation(request, context);
            sw.Stop();

            // Emit grpc.call.completed (10102)
            _logger.GrpcCallCompleted(
                serviceName, methodName, Side,
                grpcStatusCode: (int)context.Status.StatusCode,
                grpcStatusDetail: context.Status.Detail,
                durationMs: sw.Elapsed.TotalMilliseconds,
                requestSize: null, responseSize: null);

            return response;
        }
        catch (RpcException ex)
        {
            sw.Stop();
            EmitFailed(serviceName, methodName, (int)ex.StatusCode, ex.Status.Detail, sw.Elapsed.TotalMilliseconds, ex);
            throw; // Re-throw — interceptor observes, never swallows
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

    /// <summary>Intercepts server streaming calls.</summary>
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var fullMethod = context.Method;
        var serviceName = GrpcMethodParser.ExtractServiceName(fullMethod);
        var methodName = GrpcMethodParser.ExtractMethodName(fullMethod);

        if (GrpcMethodParser.IsExcluded(fullMethod, serviceName, _options))
        {
            await continuation(request, responseStream, context);
            return;
        }

        _logger.GrpcCallStarted(serviceName, methodName, Side, requestSize: null);

        IDisposable? causalScope = null;
        if (_options.EnableCausalScope)
        {
            causalScope = OtelEventsCausalityContext.SetParent(Uuid7.FormatEventId());
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await continuation(request, responseStream, context);
            sw.Stop();

            _logger.GrpcCallCompleted(
                serviceName, methodName, Side,
                grpcStatusCode: (int)context.Status.StatusCode,
                grpcStatusDetail: context.Status.Detail,
                durationMs: sw.Elapsed.TotalMilliseconds,
                requestSize: null, responseSize: null);
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

    /// <summary>Intercepts client streaming calls.</summary>
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var fullMethod = context.Method;
        var serviceName = GrpcMethodParser.ExtractServiceName(fullMethod);
        var methodName = GrpcMethodParser.ExtractMethodName(fullMethod);

        if (GrpcMethodParser.IsExcluded(fullMethod, serviceName, _options))
        {
            return await continuation(requestStream, context);
        }

        _logger.GrpcCallStarted(serviceName, methodName, Side, requestSize: null);

        IDisposable? causalScope = null;
        if (_options.EnableCausalScope)
        {
            causalScope = OtelEventsCausalityContext.SetParent(Uuid7.FormatEventId());
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await continuation(requestStream, context);
            sw.Stop();

            _logger.GrpcCallCompleted(
                serviceName, methodName, Side,
                grpcStatusCode: (int)context.Status.StatusCode,
                grpcStatusDetail: context.Status.Detail,
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

    /// <summary>Intercepts duplex streaming calls.</summary>
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var fullMethod = context.Method;
        var serviceName = GrpcMethodParser.ExtractServiceName(fullMethod);
        var methodName = GrpcMethodParser.ExtractMethodName(fullMethod);

        if (GrpcMethodParser.IsExcluded(fullMethod, serviceName, _options))
        {
            await continuation(requestStream, responseStream, context);
            return;
        }

        _logger.GrpcCallStarted(serviceName, methodName, Side, requestSize: null);

        IDisposable? causalScope = null;
        if (_options.EnableCausalScope)
        {
            causalScope = OtelEventsCausalityContext.SetParent(Uuid7.FormatEventId());
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await continuation(requestStream, responseStream, context);
            sw.Stop();

            _logger.GrpcCallCompleted(
                serviceName, methodName, Side,
                grpcStatusCode: (int)context.Status.StatusCode,
                grpcStatusDetail: context.Status.Detail,
                durationMs: sw.Elapsed.TotalMilliseconds,
                requestSize: null, responseSize: null);
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
