using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OtelEvents.Subscriptions;

/// <summary>
/// Background hosted service that reads from the dispatch channel and invokes
/// subscription handlers. Each handler invocation is wrapped in try-catch
/// so handler errors never crash the service. Individual handler calls are
/// subject to <see cref="OtelEventsSubscriptionOptions.HandlerTimeout"/>.
/// </summary>
internal sealed class OtelEventsSubscriptionDispatcher : BackgroundService
{
    private readonly Channel<DispatchItem> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OtelEventsSubscriptionDispatcher> _logger;
    private readonly OtelEventsSubscriptionOptions _options;

    public OtelEventsSubscriptionDispatcher(
        Channel<DispatchItem> channel,
        IServiceProvider serviceProvider,
        ILogger<OtelEventsSubscriptionDispatcher> logger,
        IOptions<OtelEventsSubscriptionOptions> options)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            foreach (var registration in item.Registrations)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeoutCts.CancelAfter(_options.HandlerTimeout);
                await InvokeHandlerAsync(registration, item.Context, timeoutCts.Token, stoppingToken);
            }
        }
    }

    private async Task InvokeHandlerAsync(
        SubscriptionRegistration registration,
        OtelEventContext context,
        CancellationToken cancellationToken,
        CancellationToken stoppingToken)
    {
        try
        {
            if (registration.LambdaHandler is not null)
            {
                await registration.LambdaHandler(context, cancellationToken);
            }
            else if (registration.HandlerType is not null)
            {
                using var scope = _serviceProvider.CreateScope();
                var handler = (IOtelEventHandler)scope.ServiceProvider
                    .GetRequiredService(registration.HandlerType);
                await handler.HandleAsync(context, cancellationToken);
            }

            SubscriptionMetrics.EventsDispatched.Add(1);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown — not a timeout, don't inflate metrics
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Genuine handler timeout
            _logger.LogWarning(
                "Subscription handler for pattern '{EventPattern}' timed out on event '{EventName}'",
                registration.EventPattern, context.EventName);
            SubscriptionMetrics.HandlerTimeouts.Add(1);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Subscription handler for pattern '{EventPattern}' failed on event '{EventName}'",
                registration.EventPattern, context.EventName);
            SubscriptionMetrics.HandlerErrors.Add(1);
        }
    }
}
