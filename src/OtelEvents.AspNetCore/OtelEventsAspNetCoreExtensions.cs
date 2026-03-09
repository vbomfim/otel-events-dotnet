using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace OtelEvents.AspNetCore;

/// <summary>
/// Extension methods for registering the OtelEvents.AspNetCore integration pack
/// in the ASP.NET Core dependency injection container and middleware pipeline.
/// </summary>
public static class OtelEventsAspNetCoreExtensions
{
    /// <summary>
    /// Adds OtelEvents.AspNetCore services with default options.
    /// Registers the middleware via <see cref="IStartupFilter"/> for automatic
    /// outermost-position registration in the pipeline.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddOtelEventsAspNetCore(this IServiceCollection services)
    {
        return services.AddOtelEventsAspNetCore(_ => { });
    }

    /// <summary>
    /// Adds OtelEvents.AspNetCore services with the specified options.
    /// Registers the middleware via <see cref="IStartupFilter"/> for automatic
    /// outermost-position registration in the pipeline.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Action to configure <see cref="OtelEventsAspNetCoreOptions"/>.</param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddOtelEventsAspNetCore(
        this IServiceCollection services,
        Action<OtelEventsAspNetCoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddSingleton<OtelEventsAspNetCoreMiddleware>();
        services.AddSingleton<IStartupFilter, OtelEventsAspNetCoreStartupFilter>();

        return services;
    }

    /// <summary>
    /// Manually registers the OtelEvents.AspNetCore middleware in the pipeline.
    /// Use this instead of the automatic <see cref="IStartupFilter"/> registration
    /// when explicit pipeline ordering is needed.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The <paramref name="app"/> for chaining.</returns>
    public static IApplicationBuilder UseOtelEventsAspNetCore(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<OtelEventsAspNetCoreMiddleware>();
    }
}
