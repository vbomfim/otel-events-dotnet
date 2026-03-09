using Microsoft.Extensions.DependencyInjection;

namespace OtelEvents.Grpc;

/// <summary>
/// Extension methods for registering the OtelEvents.Grpc integration pack
/// in the dependency injection container.
/// </summary>
public static class OtelEventsGrpcExtensions
{
    /// <summary>
    /// Adds OtelEvents.Grpc services with default options.
    /// Registers the server and client interceptors as singletons.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddOtelEventsGrpc(this IServiceCollection services)
    {
        return services.AddOtelEventsGrpc(_ => { });
    }

    /// <summary>
    /// Adds OtelEvents.Grpc services with the specified options.
    /// Registers the server and client interceptors as singletons.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Action to configure <see cref="OtelEventsGrpcOptions"/>.</param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddOtelEventsGrpc(
        this IServiceCollection services,
        Action<OtelEventsGrpcOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddSingleton<OtelEventsGrpcServerInterceptor>();
        services.AddSingleton<OtelEventsGrpcClientInterceptor>();

        return services;
    }
}
