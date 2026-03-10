using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OtelEvents.Subscriptions;

/// <summary>
/// Background hosted service that reads from the dispatch channel and invokes
/// subscription handlers. Each handler invocation is wrapped in try-catch
/// so handler errors never crash the service.
/// </summary>
internal sealed class OtelEventsSubscriptionDispatcher : BackgroundService
{
    private readonly Channel<DispatchItem> _channel;
    private readonly IServiceProvider _serviceProvider;

    public OtelEventsSubscriptionDispatcher(
        Channel<DispatchItem> channel,
        IServiceProvider serviceProvider)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            foreach (var registration in item.Registrations)
            {
                await InvokeHandlerAsync(registration, item.Context, stoppingToken);
            }
        }
    }

    private async Task InvokeHandlerAsync(
        SubscriptionRegistration registration,
        OtelEventContext context,
        CancellationToken cancellationToken)
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Graceful shutdown — don't meter this as an error
            throw;
        }
        catch
        {
            SubscriptionMetrics.HandlerErrors.Add(1);
        }
    }
}
