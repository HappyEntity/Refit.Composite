using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif

namespace Refit.Composite.Registration;

/// <summary>
/// Carries the arguments of a single <c>AddRefitComposite</c> call to the generated registrar
/// and exposes the building blocks the generated code is composed of.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RefitCompositeRegistrationContext
{
    internal RefitCompositeRegistrationContext(
        IServiceCollection services,
        Type compositeType,
        Uri baseAddress,
        RefitSettings? settings,
        Action<IHttpClientBuilder>? configure)
    {
        Services = services;
        CompositeType = compositeType;
        BaseAddress = baseAddress;
        Settings = settings;
        Configure = configure;
    }

    /// <summary>Gets the service collection being configured.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Gets the composite API interface. It is also the DI key of every client registered for it.</summary>
    public Type CompositeType { get; }

    /// <summary>Gets the base address applied to every client of the composite.</summary>
    public Uri BaseAddress { get; }

    /// <summary>Gets the Refit settings applied to every client of the composite.</summary>
    public RefitSettings? Settings { get; }

    /// <summary>Gets the user callback applied to every client after its handler pipeline.</summary>
    public Action<IHttpClientBuilder>? Configure { get; }

    /// <summary>
    /// Registers a message handler as a transient service unless it has already been registered.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    public void AddHandler<
#if NET
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        THandler>()
        where THandler : DelegatingHandler
    {
        Services.TryAddTransient<THandler>();
    }

    /// <summary>
    /// Adds a message handler resolved from DI to a client pipeline.
    /// Generated code calls this instead of <c>HttpClientBuilderExtensions</c> directly, so a user type with that name
    /// in the <c>Microsoft.Extensions.DependencyInjection</c> namespace cannot shadow it.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="builder">The client builder.</param>
    public static void AddMessageHandler<THandler>(IHttpClientBuilder builder)
        where THandler : DelegatingHandler
    {
        builder.AddHttpMessageHandler<THandler>();
    }

    /// <summary>
    /// Registers a source-generated Refit client keyed by <see cref="CompositeType"/>, with its own named <see cref="HttpClient"/>.
    /// </summary>
    /// <typeparam name="TClient">The Refit interface.</typeparam>
    /// <param name="pipeline">Adds the message handlers of this client, outermost first.</param>
    public void AddClient<TClient>(Action<IHttpClientBuilder> pipeline)
        where TClient : class
    {
        var compositeType = CompositeType;
        var baseAddress = BaseAddress;
        var httpClientName = $"Refit.Composite:{compositeType.FullName}:{typeof(TClient).FullName}";

        var builder = Services
            .AddKeyedRefitGeneratedClient<TClient>(compositeType, Settings, httpClientName)
            .ConfigureHttpClient(client => client.BaseAddress = baseAddress);

        pipeline(builder);
        Configure?.Invoke(builder);

        // Keep the client injectable on its own, as it was before clients became keyed.
        // When several composites share a client, the first registration wins.
        Services.TryAddTransient(sp => sp.GetRequiredKeyedService<TClient>(compositeType));
    }

    /// <summary>
    /// Registers the generated composite implementation as a Scoped service.
    /// </summary>
    /// <typeparam name="TComposite">The composite API interface.</typeparam>
    /// <typeparam name="TImplementation">The generated implementation.</typeparam>
    public void AddComposite<TComposite,
#if NET
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TImplementation>()
        where TComposite : class
        where TImplementation : class, TComposite
    {
        Services.AddScoped<TComposite, TImplementation>();
    }

    /// <summary>
    /// Returns the cached client, resolving and caching it on first access.
    /// Lock-free: a race may resolve the transient client twice, but every caller observes the same instance.
    /// </summary>
    /// <typeparam name="TClient">The Refit interface.</typeparam>
    /// <param name="field">The cache field of the generated composite.</param>
    /// <param name="serviceProvider">The scope the composite belongs to.</param>
    /// <param name="compositeType">The composite API interface, used as the DI key.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TClient GetClient<TClient>(ref TClient? field, IServiceProvider serviceProvider, Type compositeType)
        where TClient : class
    {
        return Volatile.Read(ref field) ?? ResolveClient(ref field, serviceProvider, compositeType);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TClient ResolveClient<TClient>(ref TClient? field, IServiceProvider serviceProvider, Type compositeType)
        where TClient : class
    {
        var client = serviceProvider.GetRequiredKeyedService<TClient>(compositeType);
        return Interlocked.CompareExchange(ref field, client, null) ?? client;
    }
}
