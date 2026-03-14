// <copyright file="MetricsDiIntegrationTests.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

using System.Diagnostics.Metrics;
using FluentAssertions;
using OtelEvents.Health.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OtelEvents.Health.Tests.Integration;

/// <summary>
/// DI integration tests for the metrics subsystem registration via <c>AddOtelEventsHealth()</c>.
/// Verifies that all 5 ISP-split sub-interfaces and the composed <see cref="IHealthBossMetrics"/>
/// resolve correctly as singletons pointing to the same <see cref="HealthBossMetrics"/> instance.
/// Issue #66 — Missing integration/NullObject/compat tests.
/// </summary>
public sealed class MetricsDiIntegrationTests
{
    // ─────────────────────────────────────────────────────────────────
    // Helper: Build a ServiceProvider with AddOtelEventsHealth() and one component.
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a minimal DI container with HealthBoss registered (including a NullLoggerFactory).
    /// The caller is responsible for disposing the returned <see cref="ServiceProvider"/>.
    /// </summary>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddOtelEventsHealth(opts => opts.AddComponent("test-component"));
        return services.BuildServiceProvider();
    }

    // ─────────────────────────────────────────────────────────────────
    // [DI] Sub-interface resolution — all 5 + composed
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddOtelEventsHealth_Registers_IHealthBossMetrics()
    {
        using var provider = BuildProvider();

        var metrics = provider.GetService<IHealthBossMetrics>();

        metrics.Should().NotBeNull("AddOtelEventsHealth() must register IHealthBossMetrics");
    }

    [Fact]
    public void AddOtelEventsHealth_Registers_IComponentMetrics()
    {
        using var provider = BuildProvider();

        var metrics = provider.GetService<IComponentMetrics>();

        metrics.Should().NotBeNull("AddOtelEventsHealth() must register IComponentMetrics");
    }

    [Fact]
    public void AddOtelEventsHealth_Registers_ISessionMetrics()
    {
        using var provider = BuildProvider();

        var metrics = provider.GetService<ISessionMetrics>();

        metrics.Should().NotBeNull("AddOtelEventsHealth() must register ISessionMetrics");
    }

    [Fact]
    public void AddOtelEventsHealth_Registers_IStateMachineMetrics()
    {
        using var provider = BuildProvider();

        var metrics = provider.GetService<IStateMachineMetrics>();

        metrics.Should().NotBeNull("AddOtelEventsHealth() must register IStateMachineMetrics");
    }

    [Fact]
    public void AddOtelEventsHealth_Registers_ITenantMetrics()
    {
        using var provider = BuildProvider();

        var metrics = provider.GetService<ITenantMetrics>();

        metrics.Should().NotBeNull("AddOtelEventsHealth() must register ITenantMetrics");
    }

    [Fact]
    public void AddOtelEventsHealth_Registers_IQuorumMetrics()
    {
        using var provider = BuildProvider();

        var metrics = provider.GetService<IQuorumMetrics>();

        metrics.Should().NotBeNull("AddOtelEventsHealth() must register IQuorumMetrics");
    }

    [Theory]
    [InlineData(typeof(IComponentMetrics))]
    [InlineData(typeof(ISessionMetrics))]
    [InlineData(typeof(IStateMachineMetrics))]
    [InlineData(typeof(ITenantMetrics))]
    [InlineData(typeof(IQuorumMetrics))]
    [InlineData(typeof(IHealthBossMetrics))]
    public void AddOtelEventsHealth_Registers_AllMetricsInterfaces(Type interfaceType)
    {
        using var provider = BuildProvider();

        var resolved = provider.GetService(interfaceType);

        resolved.Should().NotBeNull(
            $"AddOtelEventsHealth() must register {interfaceType.Name}");
    }

    // ─────────────────────────────────────────────────────────────────
    // [DI] Singleton identity — all interfaces point to same instance
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AllSubInterfaces_ResolveTo_SameSingleton()
    {
        using var provider = BuildProvider();

        var composed = provider.GetRequiredService<IHealthBossMetrics>();
        var component = provider.GetRequiredService<IComponentMetrics>();
        var session = provider.GetRequiredService<ISessionMetrics>();
        var stateMachine = provider.GetRequiredService<IStateMachineMetrics>();
        var tenant = provider.GetRequiredService<ITenantMetrics>();
        var quorum = provider.GetRequiredService<IQuorumMetrics>();

        component.Should().BeSameAs(composed,
            "IComponentMetrics must resolve to same singleton as IHealthBossMetrics");
        session.Should().BeSameAs(composed,
            "ISessionMetrics must resolve to same singleton as IHealthBossMetrics");
        stateMachine.Should().BeSameAs(composed,
            "IStateMachineMetrics must resolve to same singleton as IHealthBossMetrics");
        tenant.Should().BeSameAs(composed,
            "ITenantMetrics must resolve to same singleton as IHealthBossMetrics");
        quorum.Should().BeSameAs(composed,
            "IQuorumMetrics must resolve to same singleton as IHealthBossMetrics");
    }

    [Theory]
    [InlineData(typeof(IComponentMetrics))]
    [InlineData(typeof(ISessionMetrics))]
    [InlineData(typeof(IStateMachineMetrics))]
    [InlineData(typeof(ITenantMetrics))]
    [InlineData(typeof(IQuorumMetrics))]
    public void SubInterface_ResolvedTwice_ReturnsSameInstance(Type interfaceType)
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService(interfaceType);
        var second = provider.GetRequiredService(interfaceType);

        first.Should().BeSameAs(second,
            $"{interfaceType.Name} must be registered as singleton");
    }

    [Fact]
    public void IHealthBossMetrics_ResolvedTwice_ReturnsSameInstance()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IHealthBossMetrics>();
        var second = provider.GetRequiredService<IHealthBossMetrics>();

        first.Should().BeSameAs(second);
    }

    // ─────────────────────────────────────────────────────────────────
    // [DI] Concrete type — resolved instances are HealthBossMetrics
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AllSubInterfaces_ResolveAs_HealthBossMetrics_ConcreteType()
    {
        using var provider = BuildProvider();

        var composed = provider.GetRequiredService<IHealthBossMetrics>();

        composed.Should().BeOfType<HealthBossMetrics>(
            "AddOtelEventsHealth() registers the real HealthBossMetrics, not the NullObject");
    }

    // ─────────────────────────────────────────────────────────────────
    // [DI] IMeterFactory availability
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddOtelEventsHealth_Provides_IMeterFactory()
    {
        using var provider = BuildProvider();

        var meterFactory = provider.GetService<IMeterFactory>();

        meterFactory.Should().NotBeNull(
            "AddOtelEventsHealth() calls AddMetrics() which registers IMeterFactory");
    }

    [Fact]
    public void IMeterFactory_IsAvailable_AfterAddOtelEventsHealth()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddOtelEventsHealth(opts => opts.AddComponent("comp-1"));

        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IMeterFactory>();
        factory.Should().NotBeNull();

        // Verify the factory is functional — can create meters
        using var meter = factory.Create("test-meter");
        meter.Should().NotBeNull();
        meter.Name.Should().Be("test-meter");
    }

    // ─────────────────────────────────────────────────────────────────
    // [DI] OpenTelemetryMetricEventSink resolution
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddOtelEventsHealth_Registers_OpenTelemetryMetricEventSink()
    {
        using var provider = BuildProvider();

        var sink = provider.GetService<OpenTelemetryMetricEventSink>();

        sink.Should().NotBeNull(
            "AddOtelEventsHealth() must register OpenTelemetryMetricEventSink");
    }

    [Fact]
    public void OpenTelemetryMetricEventSink_Resolves_WithInjectedSubInterfaces()
    {
        using var provider = BuildProvider();

        // The sink should resolve without exception (its constructor requires
        // IStateMachineMetrics and ITenantMetrics — both must be available).
        var act = () => provider.GetRequiredService<OpenTelemetryMetricEventSink>();

        act.Should().NotThrow(
            "OpenTelemetryMetricEventSink depends on IStateMachineMetrics and ITenantMetrics, " +
            "both of which must be registered by AddOtelEventsHealth()");
    }

    [Fact]
    public void OpenTelemetryMetricEventSink_IsRegisteredAs_Singleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<OpenTelemetryMetricEventSink>();
        var second = provider.GetRequiredService<OpenTelemetryMetricEventSink>();

        first.Should().BeSameAs(second);
    }

    // ─────────────────────────────────────────────────────────────────
    // [DI] EventSinkDispatcher wiring — includes OTel sink
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EventSinkDispatcher_ResolvesSuccessfully()
    {
        using var provider = BuildProvider();

        var dispatcher = provider.GetService<IEventSinkDispatcher>();

        dispatcher.Should().NotBeNull(
            "IEventSinkDispatcher must be registered and wired through AddOtelEventsHealth()");
    }

    // ─────────────────────────────────────────────────────────────────
    // [DI] Multiple components — metrics still share singleton
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MultipleComponents_ShareSameMetrics_Singleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddOtelEventsHealth(opts =>
        {
            opts.AddComponent("redis");
            opts.AddComponent("sql-server");
            opts.AddComponent("blob-storage");
        });

        using var provider = services.BuildServiceProvider();

        var composed = provider.GetRequiredService<IHealthBossMetrics>();
        var component = provider.GetRequiredService<IComponentMetrics>();
        var session = provider.GetRequiredService<ISessionMetrics>();

        composed.Should().BeSameAs(component);
        composed.Should().BeSameAs(session);
    }
}
