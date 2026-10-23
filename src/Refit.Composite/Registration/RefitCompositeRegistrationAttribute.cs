using System.ComponentModel;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif

namespace Refit.Composite.Registration;

/// <summary>
/// Links a composite API interface to its generated <see cref="IRefitCompositeRegistrar"/>.
/// Emitted by the Refit.Composite source generator; not intended to be applied by hand.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class RefitCompositeRegistrationAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefitCompositeRegistrationAttribute"/> class.
    /// </summary>
    /// <param name="compositeType">The composite API interface.</param>
    /// <param name="registrarType">The generated registrar for <paramref name="compositeType"/>.</param>
    public RefitCompositeRegistrationAttribute(
        Type compositeType,
#if NET
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
        Type registrarType)
    {
        CompositeType = compositeType;
        RegistrarType = registrarType;
    }

    /// <summary>
    /// Gets the composite API interface.
    /// </summary>
    public Type CompositeType { get; }

    /// <summary>
    /// Gets the generated registrar type.
    /// </summary>
#if NET
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
    public Type RegistrarType { get; }
}
