using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Refit.Composite;

/// <summary>
/// A dynamic proxy implementation that intercept calls to composite API interface properties
/// and resolves the underlying Refit clients from the dependency injection container with caching.
/// </summary>
public class RefitCompositeProxy : DispatchProxy
{
    /// <summary>
    /// Gets the service provider instance used to resolve API dependencies.
    /// </summary>
    private IServiceProvider ServiceProvider { get; set; } = null!;
    
    private readonly ConcurrentDictionary<Type, object> _apiCache = new();

    /// <summary>
    /// Creates a dynamic proxy instance implementing the specified composite interface.
    /// </summary>
    /// <typeparam name="T">The composite interface type that inherits from <see cref="IRefitComposite"/>.</typeparam>
    /// <param name="serviceProvider">The application service provider used for dependency resolution.</param>
    /// <returns>A dynamically generated proxy object that implements <typeparamref name="T"/>.</returns>
    public static T Create<T>(IServiceProvider serviceProvider) where T : class, IRefitComposite
    {
        object proxy = Create<T, RefitCompositeProxy>();
        ((RefitCompositeProxy)proxy).ServiceProvider = serviceProvider;
        return (T)proxy;
    }

    /// <summary>
    /// Intercepts and processes invocations on the proxy instance methods.
    /// Only property auto-getters are supported; any other method calls will throw an exception.
    /// </summary>
    /// <param name="targetMethod">The method being invoked on the proxy.</param>
    /// <param name="args">The arguments passed to the method.</param>
    /// <returns>The resolved and cached Refit API client instance for property getters; otherwise, null.</returns>
    /// <exception cref="NotImplementedException">Thrown when a method other than a property getter is called.</exception>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null) return null;
        
        if (targetMethod.IsSpecialName && targetMethod.Name.StartsWith("get_"))
        {
            var returnType = targetMethod.ReturnType;
            
            return _apiCache.GetOrAdd(returnType, t => ServiceProvider.GetRequiredService(t));
        }

        throw new NotImplementedException(
            $"The method '{targetMethod.Name}' is not supported by the dynamic proxy. " +
            $"Ensure you only declare properties with standard auto-getters (e.g., ITestApi MyApi {{ get; }}).");
    }
}