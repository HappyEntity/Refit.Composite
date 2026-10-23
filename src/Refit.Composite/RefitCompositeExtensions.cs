using System.Net.Http;
using System.Reflection;
using Refit;
using Refit.Composite;
using Refit.Composite.Registration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering composite Refit API structures in the dependency injection container.
/// </summary>
public static class RefitCompositeExtensions
{
    /// <summary>
    /// Registers a composite Refit API interface in the dependency injection container using a <see cref="String"/> base URL.
    /// Every API property becomes a source-generated Refit client, keyed by <typeparamref name="TApi"/>, with its own
    /// <see cref="HttpClient"/> and message handler pipeline; the composite itself is registered as a Scoped service.
    /// </summary>
    /// <typeparam name="TApi">The composite interface type that inherits from <see cref="IRefitComposite"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="baseApi">The absolute base URL of the remote API.</param>
    /// <param name="settings">Optional <see cref="RefitSettings"/> to customize the Refit client behavior.</param>
    /// <param name="configure">An optional delegate to further configure the underlying <see cref="IHttpClientBuilder"/> of each API client.
    /// It runs after the handlers declared with attributes have been added.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseApi"/> is null, empty or not an absolute URI.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <typeparamref name="TApi"/> is already registered,
    /// or when no generated registration exists for it.</exception>
    public static IServiceCollection AddRefitComposite<TApi>(
        this IServiceCollection services,
        string baseApi, RefitSettings? settings = null, Action<IHttpClientBuilder>? configure = null)
        where TApi : class, IRefitComposite
    {
#if NET
        ArgumentException.ThrowIfNullOrEmpty(baseApi);
#else
        if (string.IsNullOrEmpty(baseApi)) {
            throw new ArgumentException("The base API URL cannot be null or empty.", nameof(baseApi));
        }
#endif

        if (!Uri.TryCreate(baseApi, UriKind.Absolute, out var baseUri)) {
            throw new ArgumentException($"The base API URL '{baseApi}' must be an absolute URI.", nameof(baseApi));
        }

        return services.AddRefitComposite<TApi>(baseUri, settings, configure);
    }

    /// <summary>
    /// Registers a composite Refit API interface in the dependency injection container using a <see cref="Uri"/> base URL.
    /// Every API property becomes a source-generated Refit client, keyed by <typeparamref name="TApi"/>, with its own
    /// <see cref="HttpClient"/> and message handler pipeline; the composite itself is registered as a Scoped service.
    /// </summary>
    /// <typeparam name="TApi">The composite interface type that inherits from <see cref="IRefitComposite"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="baseApi">The absolute base URL of the remote API.</param>
    /// <param name="settings">Optional <see cref="RefitSettings"/> to customize the Refit client behavior.</param>
    /// <param name="configure">An optional delegate to further configure the underlying <see cref="IHttpClientBuilder"/> of each API client.
    /// It runs after the handlers declared with attributes have been added.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseApi"/> is not an absolute URI.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <typeparamref name="TApi"/> is already registered,
    /// or when no generated registration exists for it.</exception>
    public static IServiceCollection AddRefitComposite<TApi>(
        this IServiceCollection services,
        Uri baseApi, RefitSettings? settings = null, Action<IHttpClientBuilder>? configure = null)
        where TApi : class, IRefitComposite
    {
#if NET
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseApi);
#else
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (baseApi is null) throw new ArgumentNullException(nameof(baseApi));
#endif

        if (!baseApi.IsAbsoluteUri) {
            throw new ArgumentException($"The base API URL '{baseApi}' must be an absolute URI.", nameof(baseApi));
        }

        var compositeType = typeof(TApi);

        if (services.Any(d => d.ServiceType == compositeType && !d.IsKeyedService)) {
            throw new InvalidOperationException(
                $"The composite API '{compositeType.FullName}' is already registered. Call AddRefitComposite once per composite.");
        }

        var registrar = CreateRegistrar(compositeType);
        registrar.Register(new RefitCompositeRegistrationContext(services, compositeType, baseApi, settings, configure));

        return services;
    }

    private static IRefitCompositeRegistrar CreateRegistrar(Type compositeType)
    {
        foreach (var registration in compositeType.Assembly.GetCustomAttributes<RefitCompositeRegistrationAttribute>()) {
            if (registration.CompositeType == compositeType) {
                return (IRefitCompositeRegistrar)Activator.CreateInstance(registration.RegistrarType)!;
            }
        }

        throw new InvalidOperationException(
            $"No generated registration was found for the composite API '{compositeType.FullName}'. " +
            "Make sure it is an interface inheriting IRefitComposite, that the project declaring it references HappyEntity.Refit.Composite " +
            "and builds with Roslyn 4.8+ (.NET SDK 8.0.100+), and that the build reports no RCOMP errors.");
    }
}
