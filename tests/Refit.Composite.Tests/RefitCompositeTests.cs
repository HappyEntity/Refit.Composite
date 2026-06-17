using Microsoft.Extensions.DependencyInjection;

namespace Refit.Composite.Tests;

public class RefitCompositeTests
{
    #region Test API Definitions

    public interface ITestComposite : IRefitComposite
    {
        IMockApi Mock { get; }
    }

    public interface IMockApi
    {
        [Get("/test")]
        Task<string> ExecuteAsync();
    }

    private class FakeMockApi : IMockApi
    {
        public Task<string> ExecuteAsync() => Task.FromResult(string.Empty);
    }

    #endregion

    [Fact]
    public void Generator_ShouldCreateGeneratedClass_AndRegisterItInDI()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddTransient<IMockApi>(_ => new FakeMockApi());
        services.AddRefitComposite<ITestComposite>("https://localhost");

        // Act
        var provider = services.BuildServiceProvider();
        var composite = provider.GetService<ITestComposite>();

        // Assert
        Assert.NotNull(composite);
        Assert.Equal("TestCompositeGenerated", composite.GetType().Name);
    }

    [Fact]
    public void GeneratedClass_ShouldCacheApiInstances_AndReturnSameReference()
    {
        // Arrange
        var services = new ServiceCollection();

        var mockApiInstance = new FakeMockApi();
        services.AddSingleton<IMockApi>(mockApiInstance);

        services.AddRefitComposite<ITestComposite>("https://localhost");
        var provider = services.BuildServiceProvider();
        var composite = provider.GetRequiredService<ITestComposite>();

        // Act
        var firstCall = composite.Mock;
        var secondCall = composite.Mock;

        // Assert
        Assert.NotNull(firstCall);
        Assert.Same(firstCall, secondCall);
    }

    [Fact]
    public void AddRefitComposite_ShouldRegisterGeneratedClassAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IMockApi>(_ => null!);
        services.AddRefitComposite<ITestComposite>("https://localhost");
        var provider = services.BuildServiceProvider();

        // Act
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var instance1 = scope1.ServiceProvider.GetRequiredService<ITestComposite>();
        var instance2 = scope1.ServiceProvider.GetRequiredService<ITestComposite>();
        var instance3 = scope2.ServiceProvider.GetRequiredService<ITestComposite>();

        // Assert
        Assert.Same(instance1, instance2);
        Assert.NotSame(instance1, instance3);
    }
}
