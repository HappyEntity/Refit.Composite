using Microsoft.Extensions.DependencyInjection;
using Refit.Composite.Attributes;
using Refit.Composite.Handlers;

namespace Refit.Composite.Tests;

public class PipelineBuilderTests
{
    #region Test API Definitions

    [ApiHandler<HandlerA>]
    public interface IPipelineTestComposite : IRefitComposite
    {
        IMockApi DefaultPipeline { get; }

        [ApiIgnoreAllHandlers]
        [ApiHandler<HandlerB>]
        IMockApi ClearedPipeline { get; }

        [ApiIgnoreHandler<ShortLoggingHandler>]
        IMockApi IgnoredLoggerPipeline { get; }
    }
    
    public interface IOldStyleAttributeComposite : IRefitComposite
    {
        [ApiHandler(typeof(HandlerA))]
        IMockApi OldStyle { get; }
    }
    
    public interface INewStyleAttributeComposite : IRefitComposite
    {
        [ApiHandler<HandlerA>]
        IMockApi NewStyle { get; }
    }

    public interface IMockApi
    {
        [Get("/")]
        Task Get();
    }

    public class HandlerA : DelegatingHandler
    {
    }

    public class HandlerB : DelegatingHandler
    {
    }

    #endregion

    private readonly List<Type> _stubGlobalHandlers = new()
    {
        typeof(ErrorHandlingHandler),
        typeof(ShortLoggingHandler),
        typeof(HandlerA)
    };

    [Fact]
    public void BuildPipeline_WithDefaultAttributes_ShouldIncludeGlobalHandlers()
    {
        // Arrange
        var property = typeof(IPipelineTestComposite).GetProperty(nameof(IPipelineTestComposite.DefaultPipeline))!;

        // Act
        var result = RefitCompositeExtensions.BuildPipelineForProperty(property, _stubGlobalHandlers);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(typeof(ErrorHandlingHandler), result);
        Assert.Contains(typeof(ShortLoggingHandler), result);
        Assert.Contains(typeof(HandlerA), result);
    }

    [Fact]
    public void BuildPipeline_WithApiIgnoreAllHandlers_ShouldResetAndApplyOnlySubsequentHandlers()
    {
        // Arrange
        var property = typeof(IPipelineTestComposite).GetProperty(nameof(IPipelineTestComposite.ClearedPipeline))!;

        // Act
        var result = RefitCompositeExtensions.BuildPipelineForProperty(property, _stubGlobalHandlers);

        // Assert
        var expectedSingleType = Assert.Single(result);
        Assert.Equal(typeof(HandlerB), expectedSingleType);
    }

    [Fact]
    public void BuildPipeline_WithApiIgnoreHandler_ShouldSuccessfullyRemoveSpecificHandler()
    {
        // Arrange
        var property =
            typeof(IPipelineTestComposite).GetProperty(nameof(IPipelineTestComposite.IgnoredLoggerPipeline))!;

        // Act
        var result = RefitCompositeExtensions.BuildPipelineForProperty(property, _stubGlobalHandlers);

        // Assert
        Assert.Contains(typeof(ErrorHandlingHandler), result);
        Assert.Contains(typeof(HandlerA), result);
        Assert.DoesNotContain(typeof(ShortLoggingHandler), result);
    }
    
    [Fact]
    public void BuildPipeline_WithOldStyleTypeofAttribute_ShouldSuccessfullyExtractHandler()
    {
        // Arrange
        var property = typeof(IOldStyleAttributeComposite).GetProperty(nameof(IOldStyleAttributeComposite.OldStyle))!;
        var emptyGlobalHandlers = new List<Type>();

        // Act
        var result = RefitCompositeExtensions.BuildPipelineForProperty(property, emptyGlobalHandlers);

        // Assert
        var expectedHandler = Assert.Single(result);
        Assert.Equal(typeof(HandlerA), expectedHandler);
    }

    [Fact]
    public void BuildPipeline_WithNewStyleGenericAttribute_ShouldSuccessfullyExtractHandler()
    {
        // Arrange
        var property = typeof(INewStyleAttributeComposite).GetProperty(nameof(INewStyleAttributeComposite.NewStyle))!;
        var emptyGlobalHandlers = new List<Type>();

        // Act
        var result = RefitCompositeExtensions.BuildPipelineForProperty(property, emptyGlobalHandlers);

        // Assert
        var expectedHandler = Assert.Single(result);
        Assert.Equal(typeof(HandlerA), expectedHandler);
    }
}