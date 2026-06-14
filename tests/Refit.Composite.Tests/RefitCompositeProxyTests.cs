using Microsoft.Extensions.DependencyInjection;

namespace Refit.Composite.Tests;

public class RefitCompositeProxyTests
{
    #region Test API Definitions

    public interface ISimpleComposite : IRefitComposite
    {
        IMockApi Mock { get; }
    }

    public interface IDuplicateComposite : IRefitComposite
    {
        IMockApi DirectApi { get; }
        IMockApi DuplicateApi { get; }
    }

    public interface IMockApi
    {
        [Get("/test")]
        Task<string> ExecuteAsync();
    }

    #endregion

    [Fact]
    public void Proxy_ShouldCacheApiInstances_AndReturnSameReference()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRefitComposite<ISimpleComposite>("https://localhost");
        var provider = services.BuildServiceProvider();
        var composite = provider.GetRequiredService<ISimpleComposite>();

        // Act
        var firstCall = composite.Mock;
        var secondCall = composite.Mock;

        // Assert
        Assert.NotNull(firstCall);
        Assert.Same(firstCall, secondCall);
    }

    [Fact]
    public void AddRefitComposite_WithDuplicateInterfaces_ShouldThrowInvalidDataException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            services.AddRefitComposite<IDuplicateComposite>("https://localhost")
        );

        // Assert
        Assert.Contains("contains duplicate API interface definitions", exception.Message);
    }

    [Fact]
    public void AddRefitComposite_ShouldRegisterProxyAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRefitComposite<ISimpleComposite>("https://localhost");
        var provider = services.BuildServiceProvider();

        // Act
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var instance1 = scope1.ServiceProvider.GetRequiredService<ISimpleComposite>();
        var instance2 = scope1.ServiceProvider.GetRequiredService<ISimpleComposite>();
        var instance3 = scope2.ServiceProvider.GetRequiredService<ISimpleComposite>();

        // Assert
        Assert.Same(instance1, instance2);
        Assert.NotSame(instance1, instance3);
    }
}