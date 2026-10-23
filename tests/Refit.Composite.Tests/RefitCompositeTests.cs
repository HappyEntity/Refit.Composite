using System.Net.Http;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit.Composite.Attributes;
using Refit.Composite.Handlers;

namespace Refit.Composite.Tests;

public class RefitCompositeTests
{
    #region Test API Definitions

    public interface ITestComposite : IRefitComposite
    {
        IMockApi Mock { get; }
    }

    [ApiHandler<HandlerA>]
    public interface IOtherComposite : IRefitComposite
    {
        IMockApi Mock { get; }
    }

    public interface IBaseComposite : IRefitComposite
    {
        IMockApi Mock { get; }
    }

    internal interface IDerivedComposite : IBaseComposite
    {
        IOtherApi Other { get; }
    }

    public interface IMockApi
    {
        [Get("/test")]
        Task<string> ExecuteAsync();
    }

    public interface IOtherApi
    {
        [Get("/other")]
        Task<string> ExecuteAsync([Query] string secret);
    }

    private class FakeMockApi : IMockApi
    {
        public Task<string> ExecuteAsync() => Task.FromResult("fake");
    }

    #endregion

    [Fact]
    public void AddRefitComposite_ShouldRegisterGeneratedImplementation()
    {
        var services = new ServiceCollection();
        services.AddRefitComposite<ITestComposite>("https://localhost");

        var composite = services.BuildServiceProvider().CreateScope().ServiceProvider.GetService<ITestComposite>();

        Assert.NotNull(composite);
        Assert.Equal("Refit.Composite.Generated", composite.GetType().Namespace);
    }

    [Fact]
    public void GeneratedClass_ShouldCacheApiInstances_AndReturnSameReference()
    {
        var services = new ServiceCollection();
        services.AddRefitComposite<ITestComposite>("https://localhost");
        var composite = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<ITestComposite>();

        var firstCall = composite.Mock;
        var secondCall = composite.Mock;

        Assert.NotNull(firstCall);
        Assert.Same(firstCall, secondCall);
    }

    [Fact]
    public void AddRefitComposite_ShouldRegisterGeneratedClassAsScoped()
    {
        var services = new ServiceCollection();
        services.AddRefitComposite<ITestComposite>("https://localhost");
        var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var instance1 = scope1.ServiceProvider.GetRequiredService<ITestComposite>();
        var instance2 = scope1.ServiceProvider.GetRequiredService<ITestComposite>();
        var instance3 = scope2.ServiceProvider.GetRequiredService<ITestComposite>();

        Assert.Same(instance1, instance2);
        Assert.NotSame(instance1, instance3);
    }

    [Fact]
    public async Task KeyedOverride_ShouldReplaceClientInsideComposite()
    {
        var services = new ServiceCollection();
        services.AddRefitComposite<ITestComposite>("https://localhost");
        services.AddKeyedSingleton<IMockApi>(typeof(ITestComposite), new FakeMockApi());

        var composite = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<ITestComposite>();

        Assert.Equal("fake", await composite.Mock.ExecuteAsync());
    }

    [Fact]
    public async Task SameApiInTwoComposites_ShouldKeepBaseAddressesAndPipelinesApart()
    {
        var services = new ServiceCollection();
        services.AddRefitComposite<ITestComposite>("https://first.example", configure: EchoHandler.Install());
        services.AddRefitComposite<IOtherComposite>("https://second.example", configure: EchoHandler.Install());
        var provider = services.BuildServiceProvider().CreateScope().ServiceProvider;

        var first = await provider.GetRequiredService<ITestComposite>().Mock.ExecuteAsync();
        var second = await provider.GetRequiredService<IOtherComposite>().Mock.ExecuteAsync();

        Assert.Equal("|https://first.example/test", first);
        Assert.Equal("A|https://second.example/test", second);
    }

    [Fact]
    public async Task ApiInterface_ShouldStayInjectableOnItsOwn()
    {
        var services = new ServiceCollection();
        services.AddRefitComposite<ITestComposite>("https://first.example", configure: EchoHandler.Install());
        services.AddRefitComposite<IOtherComposite>("https://second.example", configure: EchoHandler.Install());

        var api = services.BuildServiceProvider().GetRequiredService<IMockApi>();

        Assert.Equal("|https://first.example/test", await api.ExecuteAsync());
    }

    [Fact]
    public async Task InheritedComposite_ShouldExposeBaseAndDerivedProperties()
    {
        var services = new ServiceCollection();
        services.AddRefitComposite<IDerivedComposite>("https://localhost", configure: EchoHandler.Install());
        var composite = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IDerivedComposite>();

        Assert.Equal("|https://localhost/test", await composite.Mock.ExecuteAsync());
        Assert.Equal("|https://localhost/other?secret=x", await composite.Other.ExecuteAsync("x"));
    }

    [Fact]
    public void AddRefitComposite_Twice_ShouldThrow()
    {
        var services = new ServiceCollection();
        services.AddRefitComposite<ITestComposite>("https://localhost");

        Assert.Throws<InvalidOperationException>(() => services.AddRefitComposite<ITestComposite>("https://localhost"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/relative")]
    public void AddRefitComposite_WithoutAbsoluteBaseAddress_ShouldThrow(string baseApi)
    {
        var services = new ServiceCollection();

        Assert.ThrowsAny<ArgumentException>(() => services.AddRefitComposite<ITestComposite>(baseApi));
    }

    [Fact]
    public async Task ShortLoggingHandler_ShouldOmitQueryString()
    {
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(logs));
        services.AddRefitComposite<IDerivedComposite>("https://localhost", configure: EchoHandler.Install(HttpStatusCode.InternalServerError));
        var composite = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IDerivedComposite>();

        await Assert.ThrowsAsync<ApiException>(() => composite.Other.ExecuteAsync("top-secret"));

        var entry = Assert.Single(logs.Entries, e => e.Category == typeof(ShortLoggingHandler).FullName);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("https://localhost/other", entry.Message);
        Assert.DoesNotContain("top-secret", entry.Message);
    }

    [Fact]
    public async Task ShortLoggingHandler_ShouldLogFailures()
    {
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(logs));
        services.AddRefitComposite<ITestComposite>("https://localhost",
            configure: b => b.ConfigurePrimaryHttpMessageHandler(() => new FailingHandler()));
        var composite = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<ITestComposite>();

        await Assert.ThrowsAnyAsync<Exception>(() => composite.Mock.ExecuteAsync());

        var entry = Assert.Single(logs.Entries, e => e.Category == typeof(ShortLoggingHandler).FullName);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("failed", entry.Message);
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("boom");
        }
    }
}
