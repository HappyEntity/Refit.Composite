namespace Refit.Composite.Attributes;

/// <summary>
/// Registers a custom <see cref="DelegatingHandler"/> to be executed in the HttpClient pipeline.
/// Can be applied globally to the interface or directly to specific API properties.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Property, AllowMultiple = true)]
public class ApiHandlerAttribute : Attribute
{
    /// <summary>
    /// Gets the type of the target message handler.
    /// </summary>
    public Type Handler { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiHandlerAttribute"/> class.
    /// </summary>
    /// <param name="handler">The type of the handler. Must inherit from <see cref="DelegatingHandler"/>.</param>
    /// <exception cref="InvalidDataException">Thrown when the provided type does not inherit from <see cref="DelegatingHandler"/>.</exception>
    public ApiHandlerAttribute(Type handler)
    {
        if (!typeof(DelegatingHandler).IsAssignableFrom(handler))
        {
            throw new InvalidDataException($"The type '{handler.Name}' must derive from '{nameof(DelegatingHandler)}'.");
        }

        Handler = handler;
    }
}

/// <summary>
/// Registers a custom <see cref="DelegatingHandler"/> to be executed in the HttpClient pipeline using a generic type argument.
/// </summary>
/// <typeparam name="THandler">The type of the handler that inherits from <see cref="DelegatingHandler"/>.</typeparam>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Property, AllowMultiple = true)]
public class ApiHandlerAttribute<THandler> : ApiHandlerAttribute 
    where THandler : DelegatingHandler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiHandlerAttribute{THandler}"/> class.
    /// </summary>
    public ApiHandlerAttribute() : base(typeof(THandler))
    {
    }
}