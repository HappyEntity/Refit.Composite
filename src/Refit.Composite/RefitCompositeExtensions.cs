using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Refit;
using Refit.Composite;
using Refit.Composite.Attributes;
using Refit.Composite.Handlers;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering composite Refit API structures in the dependency injection container.
/// </summary>
public static class RefitCompositeExtensions
{
    /// <summary>
    /// Registers a composite Refit API interface in the dependency injection container using a <see cref="String"/> base URL.
    /// This method scans the interface properties, builds individual Refit clients with their respective message handler pipelines,
    /// and registers a dynamic proxy to orchestrate requests.
    /// </summary>
    /// <typeparam name="TApi">The composite interface type that inherits from <see cref="IRefitComposite"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="baseApi">The base URL of the remote API as a string.</param>
    /// <param name="settings">Optional <see cref="RefitSettings"/> to customize the Refit client behavior.</param>
    /// <param name="configure">An optional delegate to further configure the underlying <see cref="IHttpClientBuilder"/> for each registered API client.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseApi"/> is null or empty.</exception>
    /// <exception cref="UriFormatException">Thrown when <paramref name="baseApi"/> is not a valid URI.</exception>
    /// <exception cref="InvalidDataException">Thrown when the composite interface contains duplicate API definitions or invalid handler registrations.</exception>
#if NET5_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER
    [RequiresUnreferencedCode("Scans composite API interface properties via reflection to configure HttpClient pipelines.")]
#endif
    public static IServiceCollection AddRefitComposite<
#if NET5_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
        TApi>(
        this IServiceCollection services,
        string baseApi, RefitSettings? settings = null, Action<IHttpClientBuilder>? configure = null)
        where TApi : class, IRefitComposite
    {
#if NET7_0_OR_GREATER
        ArgumentException.ThrowIfNullOrEmpty(baseApi);
#else
        if (string.IsNullOrEmpty(baseApi))
        {
            throw new ArgumentException("The base API URL cannot be null or empty.", nameof(baseApi));
        }
#endif

        return services.AddRefitComposite<TApi>(new Uri(baseApi), settings, configure);
    }

    /// <summary>
    /// Registers a composite Refit API interface in the dependency injection container using a <see cref="Uri"/> base URL.
    /// This method scans the interface properties, builds individual Refit clients with their respective message handler pipelines,
    /// and registers a dynamic proxy to orchestrate requests.
    /// </summary>
    /// <typeparam name="TApi">The composite interface type that inherits from <see cref="IRefitComposite"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="baseApi">The base URL of the remote API as a <see cref="Uri"/>.</param>
    /// <param name="settings">Optional <see cref="RefitSettings"/> to customize the Refit client behavior.</param>
    /// <param name="configure">An optional delegate to further configure the underlying <see cref="IHttpClientBuilder"/> for each registered API client.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    /// <exception cref="InvalidDataException">Thrown when the composite interface contains duplicate API definitions or invalid handler registrations.</exception>
#if NET5_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER
    [RequiresUnreferencedCode("Scans composite API interface properties via reflection to configure HttpClient pipelines.")]
#endif
    public static IServiceCollection AddRefitComposite<
#if NET5_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
        TApi>(
        this IServiceCollection services,
        Uri baseApi, RefitSettings? settings = null, Action<IHttpClientBuilder>? configure = null)
        where TApi : class, IRefitComposite
    {
        var properties = typeof(TApi).GetProperties().Where(p => p.PropertyType.IsInterface).ToArray();

        ValidateCompositeInterface<TApi>(properties);

        services.TryAddTransient<ShortLoggingHandler>();

        var globalHandlers = GetGlobalHandlers(typeof(TApi));

        foreach (var property in properties) {
            ConfigureApiProperty(services, property, baseApi, settings, globalHandlers, configure);
        }

        var interfaceType = typeof(TApi);
        var expectedClassName = interfaceType.Name.Substring(1) + "Generated";

        var generatedType = interfaceType.Assembly.GetTypes()
            .FirstOrDefault(t => t.Name == expectedClassName && typeof(TApi).IsAssignableFrom(t));

        if (generatedType == null) {
            throw new InvalidOperationException(
                $"Refit.Composite Source Generator failed to run or produce the implementation class '{expectedClassName}' " +
                $"for interface '{interfaceType.Name}'. Please ensure the project has been built successfully.");
        }

        services.AddScoped(interfaceType, generatedType);

        return services;
    }

    private static void ValidateCompositeInterface<TApi>(PropertyInfo[] properties)
    {
        if (properties.GroupBy(x => x.PropertyType).Any(x => x.Count() > 1)) {
            throw new InvalidDataException(
                $"The composite API interface '{typeof(TApi).Name}' contains duplicate API interface definitions.");
        }
    }

    private static List<Type> GetGlobalHandlers(Type compositeType)
    {
        var handlers = compositeType
            .GetCustomAttributes<ApiHandlerAttribute>(true)
            .Select(x => x.Handler)
            .ToList();

        handlers.Add(typeof(ShortLoggingHandler));

        return handlers;
    }

    private static void ConfigureApiProperty(
        IServiceCollection services,
        PropertyInfo property,
        Uri baseApi,
        RefitSettings? settings,
        List<Type> globalHandlers,
        Action<IHttpClientBuilder>? externalConfigure)
    {
        var builder = services
            .AddRefitClient(property.PropertyType, settings)
            .ConfigureHttpClient(c => {
                c.BaseAddress = baseApi;
                c.Timeout = TimeSpan.FromMinutes(10);
            });

        var finalPipeline = BuildPipelineForProperty(property, globalHandlers);

        foreach (var handler in finalPipeline) {
            builder.AddHttpMessageHandler(sp => (DelegatingHandler)sp.GetRequiredService(handler));
        }

        externalConfigure?.Invoke(builder);
    }

    internal static List<Type> BuildPipelineForProperty(PropertyInfo property, List<Type> globalHandlers)
    {
        var activeHandlers = new List<Type>(globalHandlers);
        var propertyAttributes = property.GetCustomAttributes(typeof(Attribute), true);

        foreach (var attr in propertyAttributes) {
            switch (attr) {
                case ApiIgnoreAllHandlersAttribute:
                    activeHandlers.Clear();
                    break;

                case ApiIgnoreHandlerAttribute ignoreAttr:
                    activeHandlers.Remove(ignoreAttr.Handler);
                    break;

                case ApiHandlerAttribute handlerAttr:
                    if (!activeHandlers.Contains(handlerAttr.Handler)) {
                        activeHandlers.Add(handlerAttr.Handler);
                    }

                    break;
            }
        }

        return activeHandlers;
    }
}
