// <copyright file="HealthSignalBridgeDiTests.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OtelEvents.Health.Contracts;
using OtelEvents.Schema.Models;
using OtelEvents.Subscriptions;

namespace OtelEvents.Health.Tests;

/// <summary>
/// Integration tests for <see cref="HealthSignalBridge"/> DI wiring.
/// Verifies that the full pipeline works without manual Bind() calls —
/// the bridge lazily resolves <see cref="ISignalRecorder"/> from <see cref="IServiceProvider"/>.
/// </summary>
public sealed class HealthSignalBridgeDiTests
{
    [Fact]
    public void AddOtelEventsHealth_with_components_registers_bridge_as_singleton()
    {
        var components = new List<ComponentDefinition>
        {
            new()
            {
                Name = "orders-db",
                Signals =
                [
                    new SignalMapping { Event = "http.request.completed" }
                ]
            }
        };

        var services = new ServiceCollection();
        services.AddOtelEventsHealth(components);

        using var provider = services.BuildServiceProvider();

        var bridge = provider.GetService<HealthSignalBridge>();
        bridge.Should().NotBeNull();
    }

    [Fact]
    public void Bridge_resolves_ISignalRecorder_lazily_from_ServiceProvider()
    {
        var components = new List<ComponentDefinition>
        {
            new()
            {
                Name = "orders-db",
                Signals =
                [
                    new SignalMapping { Event = "http.request.completed" }
                ]
            }
        };

        var services = new ServiceCollection();
        services.AddOtelEventsHealth(components);

        using var provider = services.BuildServiceProvider();

        // Simulate what happens at host startup: resolve all hosted services.
        // This triggers the bridge initialization (SP injection via IHostedService factory).
        var hostedServices = provider.GetServices<IHostedService>().ToList();
        hostedServices.Should().NotBeEmpty("the bridge initializer must be registered as IHostedService");

        // Get the bridge — should now have SP injected
        var bridge = provider.GetRequiredService<HealthSignalBridge>();

        // Fire a signal through the bridge — should resolve ISignalRecorder from SP
        var depId = new DependencyId("orders-db");
        var signal = components[0].Signals[0];
        var ctx = new OtelEventContext(
            "http.request.completed",
            LogLevel.Information,
            null,
            new Dictionary<string, object?>(),
            DateTimeOffset.UtcNow,
            null, null, null);

        // This should NOT throw. The bridge lazily resolves ISignalRecorder via SP.
        var act = () => bridge.HandleSignal(depId, signal, ctx);
        act.Should().NotThrow();

        // Verify the signal reached the orchestrator
        var orchestrator = provider.GetRequiredService<IHealthOrchestrator>();
        var report = orchestrator.GetHealthReport();
        report.Dependencies.Should().ContainSingle(d => d.DependencyId == depId);
    }

    [Fact]
    public void Full_pipeline_no_manual_Bind_signals_reach_orchestrator()
    {
        // This is the CRITICAL test: end-to-end verification that
        // signals flow from bridge → ISignalRecorder → orchestrator
        // without anyone calling Bind() explicitly.
        var components = new List<ComponentDefinition>
        {
            new()
            {
                Name = "redis",
                MinimumSignals = 1, // low threshold for test
                Signals =
                [
                    new SignalMapping { Event = "cache.request.completed" },
                    new SignalMapping { Event = "cache.request.failed" }
                ]
            }
        };

        var services = new ServiceCollection();
        services.AddOtelEventsHealth(components);

        using var provider = services.BuildServiceProvider();

        // Resolve hosted services (triggers bridge SP initialization)
        _ = provider.GetServices<IHostedService>().ToList();

        var bridge = provider.GetRequiredService<HealthSignalBridge>();
        var depId = new DependencyId("redis");

        // Fire 5 success signals
        for (int i = 0; i < 5; i++)
        {
            var ctx = new OtelEventContext(
                "cache.request.completed",
                LogLevel.Information,
                null,
                new Dictionary<string, object?> { ["durationMs"] = 50.0 },
                DateTimeOffset.UtcNow.AddSeconds(i),
                null, null, null);

            bridge.HandleSignal(depId, components[0].Signals[0], ctx);
        }

        // Verify signals reached the orchestrator's buffer
        var orchestrator = provider.GetRequiredService<IHealthOrchestrator>();
        var report = orchestrator.GetHealthReport();
        report.Dependencies.Should().ContainSingle();

        var dep = report.Dependencies[0];
        dep.DependencyId.Should().Be(depId);
        dep.LatestAssessment.TotalSignals.Should().Be(5);
    }
}
