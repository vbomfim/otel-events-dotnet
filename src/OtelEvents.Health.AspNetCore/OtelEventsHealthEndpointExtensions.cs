using OtelEvents.Health;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace OtelEvents.Health.AspNetCore;

/// <summary>
/// Extension methods for mapping OtelEvents Health probe endpoints.
/// </summary>
public static class OtelEventsHealthEndpointExtensions
{
    /// <summary>
    /// Maps K8s health probe endpoints (liveness, readiness, startup).
    /// <para>
    /// Requires <see cref="OtelEvents.Health.IHealthReportProvider"/> and <see cref="OtelEvents.Health.IStartupTracker"/>
    /// to be registered in the dependency injection container (via <c>AddOtelEventsHealth()</c>).
    /// </para>
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional configuration for endpoint paths and default detail level.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapHealthEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<OtelEventsHealthEndpointOptions>? configure = null)
    {
        var options = new OtelEventsHealthEndpointOptions();
        configure?.Invoke(options);

        var detailLevel = options.DefaultDetailLevel;

        endpoints.MapGet(options.LivenessPath, (OtelEvents.Health.IHealthReportProvider provider) =>
            ProbeEndpointHandler.HandleLiveness(provider, detailLevel))
            .ExcludeFromDescription();

        endpoints.MapGet(options.ReadinessPath, (OtelEvents.Health.IHealthReportProvider provider) =>
            ProbeEndpointHandler.HandleReadiness(provider, detailLevel))
            .ExcludeFromDescription();

        endpoints.MapGet(options.StartupPath, (OtelEvents.Health.IStartupTracker tracker) =>
            ProbeEndpointHandler.HandleStartup(tracker))
            .ExcludeFromDescription();

        endpoints.MapGet(options.TenantHealthPath, (
            OtelEvents.Health.IHealthReportProvider provider,
            HttpContext context) =>
        {
            var tenantProvider = context.RequestServices.GetService<ITenantHealthProvider>();
            return TenantHealthEndpointHandler.HandleTenantHealth(tenantProvider, provider, detailLevel);
        }).ExcludeFromDescription();

        return endpoints;
    }
}
