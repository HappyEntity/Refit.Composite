using Microsoft.CodeAnalysis;

namespace Refit.Composite.SourceGenerator;

internal static class Diagnostics
{
    private const string Category = "Refit.Composite";

    public static readonly DiagnosticDescriptor UnsupportedMember = new(
        id: "RCOMP001",
        title: "Unsupported composite member",
        messageFormat: "'{0}' cannot be implemented by the composite '{1}': {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A composite API may only declare get-only, non-indexer properties whose type is a Refit interface.");

    public static readonly DiagnosticDescriptor DuplicateClient = new(
        id: "RCOMP002",
        title: "Duplicate API interface in composite",
        messageFormat: "The composite '{0}' exposes '{1}' through more than one property",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each API interface gets exactly one client per composite, so it can only be exposed once.");

    public static readonly DiagnosticDescriptor InaccessibleType = new(
        id: "RCOMP003",
        title: "Type is not accessible to generated code",
        messageFormat: "'{0}' must be public or internal (including every containing type) to be used by the composite '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Generated code lives in its own namespace, so it cannot reach private or protected types.");

    public static readonly DiagnosticDescriptor GenericComposite = new(
        id: "RCOMP004",
        title: "Generic composites are not supported",
        messageFormat: "The composite '{0}' cannot be generic",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidHandler = new(
        id: "RCOMP005",
        title: "Invalid message handler",
        messageFormat: "'{0}' must be a non-abstract class deriving from System.Net.Http.DelegatingHandler",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoRefitMethods = new(
        id: "RCOMP006",
        title: "API interface declares no Refit methods",
        messageFormat: "'{0}' declares no methods with a Refit HTTP method attribute such as [Get] or [Post]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Refit only generates clients for interfaces with at least one HTTP method; resolving this property would fail at runtime.");
}
