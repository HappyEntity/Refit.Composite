using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit.Composite.Attributes;
using Refit.Composite.Handlers;

namespace Refit.Composite.Tests;

public class PipelineBuilderTests
{
    #region Test API Definitions

    [ApiHandler<HandlerA>]
    public interface IPipelineTestComposite : IRefitComposite
    {
        IDefaultApi DefaultPipeline { get; }

        [ApiIgnoreAllHandlers]
        [ApiHandler<HandlerB>]
        IClearedApi ClearedPipeline { get; }

        [ApiIgnoreHandler<ShortLoggingHandler>]
        IIgnoredLoggerApi IgnoredLoggerPipeline { get; }

        [ApiHandler<HandlerC>]
        [ApiHandler<HandlerB>]
        IAppendedApi AppendedPipeline { get; }

        [ApiIgnoreHandler<HandlerA>]
        [ApiHandler<HandlerB>]
        [ApiHandler<HandlerA>]
        IReorderedApi ReorderedPipeline { get; }
    }

    public interface IOldStyleAttributeComposite : IRefitComposite
    {
        [ApiHandler(typeof(HandlerA))]
        [ApiHandler(typeof(HandlerB))]
        IDefaultApi OldStyle { get; }
    }

    public interface IDefaultApi
    {
        [Get("/default")]
        Task<string> Get();
    }

    public interface IClearedApi
    {
        [Get("/cleared")]
        Task<string> Get();
    }

    public interface IIgnoredLoggerApi
    {
        [Get("/ignored")]
        Task<string> Get();
    }

    public interface IAppendedApi
    {
        [Get("/appended")]
        Task<string> Get();
    }

    public interface IReorderedApi
    {
        [Get("/reordered")]
        Task<string> Get();
    }

    #endregion

    private static (IPipelineTestComposite Composite, CapturingLoggerProvider Logs) CreateComposite()
    {
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(logs));
        services.AddRefitComposite<IPipelineTestComposite>("https://localhost", configure: EchoHandler.Install());

        var composite = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IPipelineTestComposite>();
        return (composite, logs);
    }

    private static string Pipeline(string response) => response.Split('|')[0];

    [Fact]
    public async Task DefaultPipeline_ShouldRunGlobalHandlers()
    {
        var (composite, _) = CreateComposite();

        Assert.Equal("A", Pipeline(await composite.DefaultPipeline.Get()));
    }

    [Fact]
    public async Task ApiIgnoreAllHandlers_ShouldResetAndApplyOnlySubsequentHandlers()
    {
        var (composite, logs) = CreateComposite();

        Assert.Equal("B", Pipeline(await composite.ClearedPipeline.Get()));
        Assert.DoesNotContain(logs.Entries, e => e.Category == typeof(ShortLoggingHandler).FullName);
    }

    [Fact]
    public async Task ApiIgnoreHandler_ShouldRemoveOnlyThatHandler()
    {
        var (composite, logs) = CreateComposite();

        Assert.Equal("A", Pipeline(await composite.IgnoredLoggerPipeline.Get()));
        Assert.DoesNotContain(logs.Entries, e => e.Category == typeof(ShortLoggingHandler).FullName);
    }

    [Fact]
    public async Task ApiHandler_ShouldAppendInDeclarationOrder()
    {
        var (composite, _) = CreateComposite();

        Assert.Equal("A,C,B", Pipeline(await composite.AppendedPipeline.Get()));
    }

    [Fact]
    public async Task ApiIgnoreHandler_ThenApiHandler_ShouldMoveHandlerToTheEnd()
    {
        var (composite, _) = CreateComposite();

        Assert.Equal("B,A", Pipeline(await composite.ReorderedPipeline.Get()));
    }

    [Fact]
    public async Task DefaultPipeline_ShouldIncludeShortLoggingHandler()
    {
        var (composite, logs) = CreateComposite();

        await composite.DefaultPipeline.Get();

        Assert.Contains(logs.Entries, e => e.Category == typeof(ShortLoggingHandler).FullName && e.Message.Contains("/default"));
    }

    [Fact]
    public async Task OldStyleTypeofAttribute_ShouldBuildTheSamePipeline()
    {
        var services = new ServiceCollection();
        services.AddRefitComposite<IOldStyleAttributeComposite>("https://localhost", configure: EchoHandler.Install());

        var composite = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IOldStyleAttributeComposite>();

        Assert.Equal("A,B", Pipeline(await composite.OldStyle.Get()));
    }

    [Fact]
    public void Handlers_ShouldBeRegisteredAutomatically()
    {
        var services = new ServiceCollection();
        services.AddRefitComposite<IPipelineTestComposite>("https://localhost");

        Assert.Contains(services, d => d.ServiceType == typeof(HandlerA));
        Assert.Contains(services, d => d.ServiceType == typeof(HandlerB));
        Assert.Contains(services, d => d.ServiceType == typeof(HandlerC));
        Assert.Contains(services, d => d.ServiceType == typeof(ShortLoggingHandler));
    }
}
