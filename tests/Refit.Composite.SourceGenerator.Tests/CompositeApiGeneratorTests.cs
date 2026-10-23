using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Refit.Composite.SourceGenerator.Tests;

public class CompositeApiGeneratorTests
{
    private const string Usings = """
        using System.Net.Http;
        using System.Threading.Tasks;
        using Refit;
        using Refit.Composite;
        using Refit.Composite.Attributes;

        """;

    private const string Api = """
        public interface IApi
        {
            [Get("/")]
            Task<string> Get();
        }

        """;

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    private static readonly ImmutableArray<MetadataReference> References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToImmutableArray();

    private static (ImmutableArray<Diagnostic> GeneratorDiagnostics, ImmutableArray<Diagnostic> CompilationErrors, GeneratorDriverRunResult Result)
        Run(string source)
    {
        var compilation = CSharpCompilation.Create(
            "Tests",
            [CSharpSyntaxTree.ParseText(Usings + source, ParseOptions)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new CompositeApiGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics, TestContext.Current.CancellationToken);

        var errors = output.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        return (generatorDiagnostics, errors, driver.GetRunResult());
    }

    private static void AssertCompiles(string source)
    {
        var (diagnostics, errors, result) = Run(source);

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
        Assert.NotEmpty(result.GeneratedTrees);
    }

    private static Diagnostic AssertSingleDiagnostic(string source, string id)
    {
        var (diagnostics, _, result) = Run(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(id, diagnostic.Id);
        Assert.True(diagnostic.Location.IsInSource);

        if (diagnostic.Severity == DiagnosticSeverity.Error) {
            Assert.Empty(result.GeneratedTrees);
        }

        return diagnostic;
    }

    [Fact]
    public void GlobalNamespaceComposite_ShouldCompile()
    {
        AssertCompiles(Api + """
            public interface IComposite : IRefitComposite
            {
                IApi Api { get; }
            }
            """);
    }

    [Fact]
    public void InternalAndNestedComposites_WithTheSameName_ShouldCompile()
    {
        AssertCompiles(Api + """
            namespace A
            {
                public class Outer
                {
                    internal interface IComposite : IRefitComposite { IApi Api { get; } }
                }
            }

            namespace B
            {
                public class Outer
                {
                    public interface IComposite : IRefitComposite { IApi Api { get; } }
                }
            }
            """);
    }

    [Fact]
    public void UserTypesShadowingFrameworkNames_ShouldCompile()
    {
        AssertCompiles(Api + """
            namespace Microsoft.Extensions.DependencyInjection
            {
                public static class HttpClientBuilderExtensions { }
                public static class ServiceCollectionServiceExtensions { }
            }

            public class Handler : DelegatingHandler { }

            [ApiHandler<Handler>]
            public interface IComposite : IRefitComposite { IApi Api { get; } }
            """);
    }

    [Fact]
    public void PartialComposite_ShouldBeGeneratedOnce()
    {
        var (diagnostics, errors, result) = Run(Api + """
            public interface IOther { [Get("/")] Task Get(); }

            public partial interface IComposite : IRefitComposite { IApi Api { get; } }
            public partial interface IComposite { IOther Other { get; } }
            """);

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
        Assert.Single(result.GeneratedTrees);
    }

    [Fact]
    public void InheritedAndHiddenProperties_ShouldCompile()
    {
        AssertCompiles(Api + """
            public interface IOther { [Get("/")] Task Get(); }

            public interface IBase : IRefitComposite { IApi Api { get; } }
            public interface IDerived : IBase
            {
                new IApi Api { get; }
                IOther? Other { get; }
            }
            """);
    }

    [Fact]
    public void DefaultImplementedMembers_ShouldBeIgnored()
    {
        AssertCompiles(Api + """
            public interface IComposite : IRefitComposite
            {
                IApi Api { get; }
                string Describe() => "composite";
            }
            """);
    }

    [Fact]
    public void Method_ShouldReportUnsupportedMember()
    {
        AssertSingleDiagnostic(Api + """
            public interface IComposite : IRefitComposite
            {
                IApi Api { get; }
                void Reset();
            }
            """, "RCOMP001");
    }

    [Fact]
    public void Setter_ShouldReportUnsupportedMember()
    {
        AssertSingleDiagnostic(Api + """
            public interface IComposite : IRefitComposite { IApi Api { get; set; } }
            """, "RCOMP001");
    }

    [Fact]
    public void NonInterfaceProperty_ShouldReportUnsupportedMember()
    {
        AssertSingleDiagnostic(Api + """
            public interface IComposite : IRefitComposite { string Name { get; } }
            """, "RCOMP001");
    }

    [Fact]
    public void DuplicateApi_ShouldReportDuplicateClient()
    {
        AssertSingleDiagnostic(Api + """
            public interface IComposite : IRefitComposite
            {
                IApi First { get; }
                IApi Second { get; }
            }
            """, "RCOMP002");
    }

    [Fact]
    public void PrivateNestedComposite_ShouldReportInaccessibleType()
    {
        AssertSingleDiagnostic(Api + """
            public class Outer
            {
                private interface IComposite : IRefitComposite { IApi Api { get; } }
            }
            """, "RCOMP003");
    }

    [Fact]
    public void GenericComposite_ShouldReportUnsupported()
    {
        AssertSingleDiagnostic(Api + """
            public interface IComposite<T> : IRefitComposite { IApi Api { get; } }
            """, "RCOMP004");
    }

    [Fact]
    public void AbstractHandler_ShouldReportInvalidHandler()
    {
        AssertSingleDiagnostic(Api + """
            public abstract class AbstractHandler : DelegatingHandler { }

            [ApiHandler<AbstractHandler>]
            public interface IComposite : IRefitComposite { IApi Api { get; } }
            """, "RCOMP005");
    }

    [Fact]
    public void NonHandlerType_ShouldReportInvalidHandler()
    {
        AssertSingleDiagnostic(Api + """
            public interface IComposite : IRefitComposite
            {
                [ApiHandler(typeof(string))]
                IApi Api { get; }
            }
            """, "RCOMP005");
    }

    [Fact]
    public void ApiWithoutRefitMethods_ShouldWarnAndStillGenerate()
    {
        var (diagnostics, errors, result) = Run("""
            public interface IEmpty { }
            public interface IComposite : IRefitComposite { IEmpty Empty { get; } }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("RCOMP006", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Empty(errors);
        Assert.Single(result.GeneratedTrees);
    }

    [Fact]
    public void UnrelatedEdit_ShouldReuseCachedModel()
    {
        var source = Usings + Api + """
            public interface IComposite : IRefitComposite { IApi Api { get; } }
            """;

        var ct = TestContext.Current.CancellationToken;
        var compilation = CSharpCompilation.Create(
            "Tests",
            [CSharpSyntaxTree.ParseText(source, ParseOptions, cancellationToken: ct)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new CompositeApiGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation, ct);
        driver = driver.RunGenerators(compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Unrelated { }", ParseOptions, cancellationToken: ct)), ct);

        var steps = driver.GetRunResult().Results.Single().TrackedSteps[CompositeApiGenerator.TrackingNames.Composites];
        Assert.All(steps.SelectMany(s => s.Outputs), output =>
            Assert.True(output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, output.Reason.ToString()));
    }
}
