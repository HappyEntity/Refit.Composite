using System.ComponentModel;

namespace Refit.Composite.Registration;

/// <summary>
/// Registers one composite API and its Refit clients. Implemented by the Refit.Composite source generator;
/// not intended to be implemented by hand.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRefitCompositeRegistrar
{
    /// <summary>
    /// Adds the composite implementation, its Refit clients and their message handler pipelines.
    /// </summary>
    /// <param name="context">The registration being performed.</param>
    void Register(RefitCompositeRegistrationContext context);
}
