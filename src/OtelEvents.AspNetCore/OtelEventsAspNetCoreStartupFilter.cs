using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace OtelEvents.AspNetCore;

/// <summary>
/// Startup filter that registers <see cref="OtelEventsAspNetCoreMiddleware"/> at the
/// outermost position in the ASP.NET Core middleware pipeline.
/// </summary>
/// <remarks>
/// This ensures the middleware captures the full request lifecycle, including
/// exceptions thrown by other middleware registered later in the pipeline.
/// The filter runs before <c>Configure</c>, making the middleware registration automatic.
/// </remarks>
internal sealed class OtelEventsAspNetCoreStartupFilter : IStartupFilter
{
    /// <summary>
    /// Configures the application builder to use OtelEvents middleware first.
    /// </summary>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return builder =>
        {
            builder.UseMiddleware<OtelEventsAspNetCoreMiddleware>();
            next(builder);
        };
    }
}
