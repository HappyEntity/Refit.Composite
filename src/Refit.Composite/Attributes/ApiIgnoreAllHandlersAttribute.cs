namespace Refit.Composite.Attributes;

/// <summary>
/// Resets the HttpClient pipeline by clearing all message handlers accumulated up to this point 
/// (including global and previous property-level handlers). Handlers declared below this attribute will still be applied.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ApiIgnoreAllHandlersAttribute : Attribute
{
}