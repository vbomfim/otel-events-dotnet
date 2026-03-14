using System.Net;
using System.Text.Json;
using FluentAssertions;
using OtelEvents.Health;
using OtelEvents.Health.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static OtelEvents.Health.AspNetCore.Tests.TestFixtures;

namespace OtelEvents.Health.AspNetCore.Tests;

/// <summary>
/// Integration tests for the tenant health endpoint (/healthz/tenants).
/// Covers detail level gating, JSON shape, and DI scenarios.
/// </summary>
public sealed class TenantHealthEndpointIntegrationTests : IAsyncDisposable
{
    // ── Detail Level Gating (Security Finding #6) ─────────────

    [Fact]
    public async Task TenantHealth_returns_403_at_StatusOnly()
    {
        await using var env = await CreateTestEnv(configure: opts =>
        {
            opts.DefaultDetailLevel = DetailLevel.StatusOnly;
        });

        var response = await env.Client.GetAsync("/healthz/tenants");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("tenant_health_requires_full_detail_level");
    }

    [Fact]
    public async Task TenantHealth_returns_403_at_Summary()
    {
        await using var env = await CreateTestEnv(configure: opts =>
        {
            opts.DefaultDetailLevel = DetailLevel.Summary;
        });

        var response = await env.Client.GetAsync("/healthz/tenants");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("tenant_health_requires_full_detail_level");
    }

    [Fact]
    public async Task TenantHealth_returns_200_at_Full()
    {
        var tenantProvider = new FakeTenantHealthProvider();
        tenantProvider.SetTenants(DbDependency, new Dictionary<TenantId, TenantHealthAssessment>
        {
            [new TenantId("contoso")] = new(new TenantId("contoso"), DbDependency, TenantHealthStatus.Degraded, 0.75, 100, 25, null),
        });

        await using var env = await CreateTestEnv(
            tenantProvider: tenantProvider,
            configure: opts => { opts.DefaultDetailLevel = DetailLevel.Full; });

        var response = await env.Client.GetAsync("/healthz/tenants");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Response Shape at Full Detail ─────────────────────────

    [Fact]
    public async Task TenantHealth_Full_response_has_correct_json_shape()
    {
        var tenantProvider = new FakeTenantHealthProvider();
        tenantProvider.SetTenants(DbDependency, new Dictionary<TenantId, TenantHealthAssessment>
        {
            [new TenantId("contoso")] = new(new TenantId("contoso"), DbDependency, TenantHealthStatus.Degraded, 0.75, 100, 25, null),
        });

        await using var env = await CreateTestEnv(
            tenantProvider: tenantProvider,
            configure: opts => { opts.DefaultDetailLevel = DetailLevel.Full; });

        var response = await env.Client.GetAsync("/healthz/tenants");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var components = root.GetProperty("components");
        components.GetArrayLength().Should().Be(2);

        var firstComponent = components[0];
        firstComponent.GetProperty("component").GetString().Should().Be("postgres-main");
        firstComponent.GetProperty("active_tenants").GetInt32().Should().Be(1);

        var tenants = firstComponent.GetProperty("tenants");
        tenants.GetArrayLength().Should().Be(1);
        tenants[0].GetProperty("tenant_id").GetString().Should().Be("contoso");
        tenants[0].GetProperty("status").GetString().Should().Be("degraded");
        tenants[0].GetProperty("success_rate").GetDouble().Should().Be(0.75);
        tenants[0].GetProperty("total_signals").GetInt32().Should().Be(100);
        tenants[0].GetProperty("failure_count").GetInt32().Should().Be(25);
    }

    [Fact]
    public async Task TenantHealth_Full_response_uses_snake_case_keys()
    {
        var tenantProvider = new FakeTenantHealthProvider();
        tenantProvider.SetTenants(DbDependency, new Dictionary<TenantId, TenantHealthAssessment>
        {
            [new TenantId("contoso")] = new(new TenantId("contoso"), DbDependency, TenantHealthStatus.Healthy, 0.99, 100, 1, null),
        });

        await using var env = await CreateTestEnv(
            tenantProvider: tenantProvider,
            configure: opts => { opts.DefaultDetailLevel = DetailLevel.Full; });

        var response = await env.Client.GetAsync("/healthz/tenants");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("active_tenants");
        body.Should().Contain("tenant_id");
        body.Should().Contain("success_rate");
        body.Should().Contain("total_signals");
        body.Should().Contain("failure_count");
        body.Should().NotContain("ActiveTenants");
        body.Should().NotContain("TenantId");
        body.Should().NotContain("SuccessRate");
    }

    [Fact]
    public async Task TenantHealth_response_content_type_is_application_json()
    {
        await using var env = await CreateTestEnv(configure: opts =>
        {
            opts.DefaultDetailLevel = DetailLevel.Full;
        });

        var response = await env.Client.GetAsync("/healthz/tenants");

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    // ── No Tenant Provider Registered ─────────────────────────

    [Fact]
    public async Task TenantHealth_no_provider_registered_returns_empty_tenants()
    {
        await using var env = await CreateTestEnv(
            tenantProvider: null, // explicitly no provider
            configure: opts => { opts.DefaultDetailLevel = DetailLevel.Full; });

        var response = await env.Client.GetAsync("/healthz/tenants");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(body);
        var components = doc.RootElement.GetProperty("components");
        components.GetArrayLength().Should().Be(2);

        // Both components should have 0 active tenants and empty tenant arrays
        components[0].GetProperty("active_tenants").GetInt32().Should().Be(0);
        components[0].GetProperty("tenants").GetArrayLength().Should().Be(0);
        components[1].GetProperty("active_tenants").GetInt32().Should().Be(0);
        components[1].GetProperty("tenants").GetArrayLength().Should().Be(0);
    }

    // ── Multiple Components Different Tenant Counts ───────────

    [Fact]
    public async Task TenantHealth_multiple_components_with_different_tenant_counts()
    {
        var tenantProvider = new FakeTenantHealthProvider();
        tenantProvider.SetTenants(DbDependency, new Dictionary<TenantId, TenantHealthAssessment>
        {
            [new TenantId("contoso")] = new(new TenantId("contoso"), DbDependency, TenantHealthStatus.Healthy, 0.99, 200, 2, null),
            [new TenantId("fabrikam")] = new(new TenantId("fabrikam"), DbDependency, TenantHealthStatus.Degraded, 0.80, 100, 20, null),
        });
        tenantProvider.SetTenants(CacheDependency, new Dictionary<TenantId, TenantHealthAssessment>
        {
            [new TenantId("northwind")] = new(new TenantId("northwind"), CacheDependency, TenantHealthStatus.Healthy, 1.0, 50, 0, null),
        });

        await using var env = await CreateTestEnv(
            tenantProvider: tenantProvider,
            configure: opts => { opts.DefaultDetailLevel = DetailLevel.Full; });

        var response = await env.Client.GetAsync("/healthz/tenants");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var components = doc.RootElement.GetProperty("components");

        components[0].GetProperty("active_tenants").GetInt32().Should().Be(2);
        components[0].GetProperty("tenants").GetArrayLength().Should().Be(2);
        components[1].GetProperty("active_tenants").GetInt32().Should().Be(1);
        components[1].GetProperty("tenants").GetArrayLength().Should().Be(1);
    }

    // ── Custom Tenant Health Path ─────────────────────────────

    [Fact]
    public async Task TenantHealth_custom_path_is_honored()
    {
        await using var env = await CreateTestEnv(configure: opts =>
        {
            opts.TenantHealthPath = "/custom/tenants";
            opts.DefaultDetailLevel = DetailLevel.Full;
        });

        var response = await env.Client.GetAsync("/custom/tenants");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var defaultPathResponse = await env.Client.GetAsync("/healthz/tenants");
        defaultPathResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Error Handling ────────────────────────────────────────

    [Fact]
    public async Task TenantHealth_exception_does_not_leak()
    {
        var provider = new ThrowingHealthReportProvider();
        await using var env = await CreateTestEnv(
            provider: provider,
            configure: opts => { opts.DefaultDetailLevel = DetailLevel.Full; });

        var response = await env.Client.GetAsync("/healthz/tenants");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        body.Should().Be("""{"status":"unhealthy"}""");
        body.Should().NotContain("Exception");
        body.Should().NotContain("Simulated");
    }

    // ── Thread Safety: Concurrent Reads ───────────────────────

    [Fact]
    public async Task TenantHealth_concurrent_reads_do_not_throw()
    {
        var tenantProvider = new FakeTenantHealthProvider();
        tenantProvider.SetTenants(DbDependency, new Dictionary<TenantId, TenantHealthAssessment>
        {
            [new TenantId("contoso")] = new(new TenantId("contoso"), DbDependency, TenantHealthStatus.Healthy, 0.99, 100, 1, null),
        });

        await using var env = await CreateTestEnv(
            tenantProvider: tenantProvider,
            configure: opts => { opts.DefaultDetailLevel = DetailLevel.Full; });

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => env.Client.GetAsync("/healthz/tenants"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
    }

    // ── Existing Endpoints Still Work ─────────────────────────

    [Fact]
    public async Task Existing_endpoints_still_work_after_tenant_endpoint_added()
    {
        await using var env = await CreateTestEnv(configure: opts =>
        {
            opts.DefaultDetailLevel = DetailLevel.Full;
        });

        var liveness = await env.Client.GetAsync("/healthz/live");
        var readiness = await env.Client.GetAsync("/healthz/ready");
        var startup = await env.Client.GetAsync("/healthz/startup");

        liveness.StatusCode.Should().Be(HttpStatusCode.OK);
        readiness.StatusCode.Should().Be(HttpStatusCode.OK);
        startup.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Test Infrastructure ───────────────────────────────────

    private readonly List<IAsyncDisposable> _disposables = [];

    public async ValueTask DisposeAsync()
    {
        foreach (var disposable in _disposables)
        {
            await disposable.DisposeAsync();
        }
    }

    private async Task<TestEnv> CreateTestEnv(
        OtelEvents.Health.IHealthReportProvider? provider = null,
        ITenantHealthProvider? tenantProvider = null,
        OtelEvents.Health.IStartupTracker? tracker = null,
        Action<OtelEventsHealthEndpointOptions>? configure = null)
    {
        provider ??= new FakeHealthReportProvider();
        tracker ??= new FakeStartupTracker();

        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<OtelEvents.Health.IHealthReportProvider>(provider);
        builder.Services.AddSingleton<OtelEvents.Health.IStartupTracker>(tracker);

        if (tenantProvider is not null)
        {
            builder.Services.AddSingleton(tenantProvider);
        }

        var app = builder.Build();
        app.MapHealthEndpoints(configure);
        await app.StartAsync();

        var client = app.GetTestClient();
        var env = new TestEnv(client, app);
        _disposables.Add(env);
        return env;
    }

    private sealed class TestEnv(HttpClient client, WebApplication app) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
