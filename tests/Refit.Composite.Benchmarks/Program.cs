using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;

namespace Refit.Composite.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<ApiResolveBenchmark>();
    }
}

[MemoryDiagnoser]
public class ApiResolveBenchmark
{
    private IServiceProvider _serviceProvider = null!;
    private IBenchmarkComposite _compositeApi = null!;
    private OldRefitApi _oldClassApi = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();

        services.AddTransient<IMockApi>(_ => new FakeMockApi());

        services.AddRefitComposite<IBenchmarkComposite>("https://localhost");

        services.AddScoped<OldRefitApi>();

        _serviceProvider = services.BuildServiceProvider();

        _compositeApi = _serviceProvider.GetRequiredService<IBenchmarkComposite>();
        _oldClassApi = _serviceProvider.GetRequiredService<OldRefitApi>();
    }

    [Benchmark(Baseline = true)]
    public IMockApi DirectDI_Resolve()
    {
        return _serviceProvider.GetRequiredService<IMockApi>();
    }

    [Benchmark]
    public IMockApi OldClass_Dictionary_Resolve()
    {
        return _oldClassApi.Mock;
    }

    [Benchmark]
    public IMockApi SourceGenerator_LockFree_ZeroAlloc_Resolve()
    {
        return _compositeApi.Mock;
    }
}

public interface IBenchmarkComposite : IRefitComposite
{
    IMockApi Mock { get; }
}

public interface IMockApi
{
    [Get("/")]
    Task Get();
}

public class FakeMockApi : IMockApi
{
    public Task Get() => Task.CompletedTask;
}

public class OldRefitApi(IServiceProvider serviceProvider)
{
    private readonly Dictionary<Type, object> _apis = new();

    public IMockApi Mock
    {
        get
        {
            if (!_apis.ContainsKey(typeof(IMockApi)))
                _apis.Add(typeof(IMockApi), serviceProvider.GetRequiredService<IMockApi>());

            return (IMockApi)_apis[typeof(IMockApi)];
        }
    }
}


