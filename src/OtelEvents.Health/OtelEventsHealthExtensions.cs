// <copyright file="OtelEventsHealthExtensions.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

using OtelEvents.Health.Components;
using OtelEvents.Health.Contracts;
using OtelEvents.Schema.Models;
using OtelEvents.Schema.Parsing;
using OtelEvents.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OtelEvents.Health;

/// <summary>
/// Extension methods for registering HealthBoss services in the dependency injection container.
/// </summary>
public static class OtelEventsHealthExtensions
{
    /// <summary>
    /// Adds the HealthBoss health intelligence layer to the service collection.
    /// Registers all core services (clock, evaluator, state graph, transition engine,
    /// startup tracker, orchestrator) as singletons, plus per-component keyed
    /// <see cref="ISignalBuffer"/> instances.
    /// Configuration is validated at registration time — invalid policies cause immediate exceptions.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">The configuration action for <see cref="HealthBossOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when any component configuration fails validation.</exception>
    public static IServiceCollection AddOtelEventsHealth(
        this IServiceCollection services,
        Action<HealthBossOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new HealthBossOptions();
        configure(options);

        // Register options as IOptions<HealthBossOptions> singleton
        services.AddSingleton<IOptions<HealthBossOptions>>(Options.Create(options));

        // Determine the time provider (custom or system default)
        var timeProvider = options.TimeProvider ?? TimeProvider.System;

        // Metrics — AddMetrics() registers IMeterFactory (idempotent — safe to call multiple times).
        // HealthBossMetrics resolves IMeterFactory via constructor injection.
        services.AddMetrics();
        services.AddSingleton<HealthBossMetrics>();
        services.AddSingleton<IHealthBossMetrics>(sp => sp.GetRequiredService<HealthBossMetrics>());
        services.AddSingleton<IComponentMetrics>(sp => sp.GetRequiredService<HealthBossMetrics>());
        services.AddSingleton<ISessionMetrics>(sp => sp.GetRequiredService<HealthBossMetrics>());
        services.AddSingleton<IStateMachineMetrics>(sp => sp.GetRequiredService<HealthBossMetrics>());
        services.AddSingleton<ITenantMetrics>(sp => sp.GetRequiredService<HealthBossMetrics>());
        services.AddSingleton<IQuorumMetrics>(sp => sp.GetRequiredService<HealthBossMetrics>());

        // Core singleton services
        services.AddSingleton<ISystemClock>(_ => new SystemClock(timeProvider));
        services.AddSingleton<IPolicyEvaluator>(_ => new PolicyEvaluator());
        services.AddSingleton<IStateGraph>(_ => new DefaultStateGraph());
        services.AddSingleton<ITransitionEngine>(sp =>
            new TransitionEngine(sp.GetRequiredService<IStateGraph>()));
        services.AddSingleton<IStartupTracker>(_ => new StartupTracker());
        services.AddSingleton<ITimerBudgetValidator>(_ => new TimerBudgetValidator());

        // Per-component keyed signal buffers
        foreach (var (name, _) in options.Components)
        {
            services.AddKeyedSingleton<ISignalBuffer>(name, (sp, _) =>
                new SignalBuffer(sp.GetRequiredService<ISystemClock>()));
        }

        // ISP: register ISignalWriter as an alias so write-only consumers
        // (gRPC interceptors, Polly hooks, recovery probers) can resolve
        // the narrow interface without depending on full ISignalBuffer.
        foreach (var (name, _) in options.Components)
        {
            services.AddKeyedSingleton<ISignalWriter>(name, (sp, key) =>
                sp.GetRequiredKeyedService<ISignalBuffer>(key));
        }

        // Build per-dependency monitors and register the orchestrator
        services.AddSingleton<IHealthOrchestrator>(sp =>
        {
            var clock = sp.GetRequiredService<ISystemClock>();
            var evaluator = sp.GetRequiredService<IPolicyEvaluator>();
            var transitionEngine = sp.GetRequiredService<ITransitionEngine>();
            var startupTracker = sp.GetRequiredService<IStartupTracker>();

            var monitors = new Dictionary<DependencyId, IDependencyMonitor>();
            foreach (var (name, registration) in options.Components)
            {
                var buffer = sp.GetRequiredKeyedService<ISignalBuffer>(name);
                var monitor = new DependencyMonitor(
                    registration.DependencyId,
                    buffer,
                    evaluator,
                    transitionEngine,
                    registration.Policy,
                    clock);
                monitors[registration.DependencyId] = monitor;
            }

            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger<HealthOrchestrator>();
            var metrics = sp.GetService<IComponentMetrics>();

            return new HealthOrchestrator(
                monitors,
                options.AggregateHealthResolver,
                options.AggregateReadinessResolver,
                startupTracker,
                clock,
                logger,
                metrics);
        });

        // Forward-register interface projections so consumers can resolve either interface
        services.AddSingleton<IHealthStateReader>(sp =>
            sp.GetRequiredService<IHealthOrchestrator>());
        services.AddSingleton<IHealthReportProvider>(sp =>
            sp.GetRequiredService<IHealthOrchestrator>());
        services.AddSingleton<ISignalRecorder>(sp =>
            sp.GetRequiredService<IHealthOrchestrator>());

        // Session health tracker — active session gauge for SIGTERM drain decisions
        services.AddSingleton<ISessionHealthTracker>(sp =>
            new SessionHealthTracker(
                sp.GetRequiredService<ISystemClock>(),
                metrics: sp.GetService<ISessionMetrics>()));

        // Quorum evaluator — instance-level health assessment for gRPC/load-balanced backends
        services.AddSingleton<IQuorumEvaluator, QuorumEvaluator>();

        // Event sink pipeline: concrete sinks → dispatcher (fan-out with rate limiting)
        // The dispatcher implements IHealthEventSink itself for orchestrator integration,
        // but must NOT appear in its own sink collection (it IS the dispatcher, not a leaf sink).
        // OpenTelemetryMetricEventSink is always included as a default sink.
        // Consumers add custom sinks via options.AddEventSink<T>() or options.AddEventSink(factory).
        services.AddSingleton<OpenTelemetryMetricEventSink>(sp =>
            new OpenTelemetryMetricEventSink(
                sp.GetRequiredService<IStateMachineMetrics>(),
                sp.GetRequiredService<ITenantMetrics>()));

        services.AddSingleton<EventSinkDispatcher>(sp =>
        {
            var sinks = new List<IHealthEventSink>
            {
                sp.GetRequiredService<OpenTelemetryMetricEventSink>(),
            };

            // Resolve consumer-registered sinks via options (avoids circular DI)
            foreach (var factory in options.EventSinkFactories)
            {
                sinks.Add(factory(sp));
            }

            return new EventSinkDispatcher(
                sinks,
                new EventSinkDispatcherOptions(),
                sp.GetRequiredService<ISystemClock>(),
                sp.GetService<ILoggerFactory>()?.CreateLogger<EventSinkDispatcher>(),
                sp.GetService<IStateMachineMetrics>());
        });

        services.AddSingleton<IEventSinkDispatcher>(sp =>
            sp.GetRequiredService<EventSinkDispatcher>());
        services.AddSingleton<IHealthEventSink>(sp =>
            sp.GetRequiredService<EventSinkDispatcher>());

        return services;
    }

    /// <summary>
    /// Adds the HealthBoss health intelligence layer with YAML-based auto-subscription bridge.
    /// Parses the schema file, registers health components, and sets up otel-events
    /// subscriptions that automatically feed health signals to the state machine.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="schemaPath">Path to the YAML schema file containing component definitions.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the schema file cannot be parsed.</exception>
    public static IServiceCollection AddOtelEventsHealth(
        this IServiceCollection services,
        string schemaPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(schemaPath);

        var parser = new SchemaParser();
        var result = parser.ParseFile(schemaPath);

        if (!result.IsSuccess)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Message));
            throw new InvalidOperationException(
                $"Failed to parse health schema '{schemaPath}': {errors}");
        }

        return services.AddOtelEventsHealth(result.Document!.Components);
    }

    /// <summary>
    /// Adds the HealthBoss health intelligence layer with auto-subscription bridge
    /// using pre-parsed component definitions.
    /// Registers health components from the definitions and sets up otel-events
    /// subscriptions that automatically feed health signals to the state machine.
    /// <para>
    /// This overload internally calls <see cref="AddOtelEventsHealth(IServiceCollection, Action{HealthBossOptions})"/>
    /// and <see cref="OtelEventsSubscriptionExtensions.AddOtelEventsSubscriptions"/> — do not call them separately.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="components">Component definitions parsed from YAML.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOtelEventsHealth(
        this IServiceCollection services,
        IReadOnlyList<ComponentDefinition> components)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(components);

        // 1. Register core health system with component configurations from YAML
        services.AddOtelEventsHealth(opts =>
        {
            foreach (var comp in components)
            {
                opts.AddComponent(comp.Name, builder =>
                {
                    if (comp.WindowSeconds > 0)
                    {
                        builder.Window(TimeSpan.FromSeconds(comp.WindowSeconds));
                    }

                    if (comp.HealthyAbove > 0)
                    {
                        builder.HealthyAbove(comp.HealthyAbove);
                    }

                    if (comp.DegradedAbove > 0)
                    {
                        builder.DegradedAbove(comp.DegradedAbove);
                    }

                    if (comp.MinimumSignals > 0)
                    {
                        builder.MinimumSignals(comp.MinimumSignals);
                    }

                    if (comp.ResponseTime is not null)
                    {
                        builder.WithResponseTime(rt =>
                        {
                            rt.Percentile(comp.ResponseTime.Percentile);
                            rt.DegradedAfter(TimeSpan.FromMilliseconds(comp.ResponseTime.DegradedAfterMs));

                            if (comp.ResponseTime.UnhealthyAfterMs > 0)
                            {
                                rt.UnhealthyAfter(TimeSpan.FromMilliseconds(comp.ResponseTime.UnhealthyAfterMs));
                            }
                        });
                    }
                });
            }
        });

        // 2. Create bridge and register subscriptions for components with signals
        var signalComponents = components.Where(c => c.Signals.Count > 0).ToList();
        if (signalComponents.Count > 0)
        {
            var bridge = new HealthSignalBridge(signalComponents);
            services.AddSingleton(bridge);

            services.AddOtelEventsSubscriptions(subs =>
            {
                bridge.RegisterSubscriptions(subs);
            });

            // 3. Override ISignalRecorder registration to also bind the bridge.
            // DI uses the last registration for GetRequiredService<T>(),
            // so this effectively decorates the original with bridge binding.
            services.AddSingleton<ISignalRecorder>(sp =>
            {
                var recorder = (ISignalRecorder)sp.GetRequiredService<IHealthOrchestrator>();
                bridge.Bind(recorder);
                return recorder;
            });
        }

        return services;
    }
}
