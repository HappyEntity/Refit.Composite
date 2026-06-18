using System.Net.Http;

namespace Refit.Composite.Attributes;

/// <summary>
/// Excludes a specific <see cref="DelegatingHandler"/> from the current HttpClient pipeline
/// if it was previously added by global configuration or prior attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class ApiIgnoreHandlerAttribute : Attribute
{
    /// <summary>
    /// Gets the type of the message handler to be ignored.
    /// </summary>
    public Type Handler { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiIgnoreHandlerAttribute"/> class.
    /// </summary>
    /// <param name="handler">The type of the handler to exclude.</param>
    public ApiIgnoreHandlerAttribute(Type handler)
    {
        Handler = handler;
    }
}

/// <summary>
/// Excludes a specific <see cref="DelegatingHandler"/> from the current HttpClient pipeline
/// if it was previously added by global configuration or prior attributes.
/// </summary>
/// <typeparam name="THandler">The type of the handler to exclude.</typeparam>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class ApiIgnoreHandlerAttribute<THandler> : ApiIgnoreHandlerAttribute
    where THandler : DelegatingHandler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiIgnoreHandlerAttribute{THandler}"/> class.
    /// </summary>
    public ApiIgnoreHandlerAttribute() : base(typeof(THandler))
    {
    }
}
