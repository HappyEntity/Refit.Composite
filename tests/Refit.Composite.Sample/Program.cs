using Microsoft.Extensions.DependencyInjection;
using Refit;
using Refit.Composite;
using Refit.Composite.Attributes;

var services = new ServiceCollection();

services.AddTransient<AntiforgeryHandler>();
services.AddRefitComposite<ITestComposite>("https://jsonplaceholder.typicode.com");

var provider = services.BuildServiceProvider().CreateScope().ServiceProvider;

var testApi = provider.GetRequiredService<ITestComposite>();

Console.WriteLine(await testApi.Test.Test());

[ApiHandler<AntiforgeryHandler>]
public interface ITestComposite : IRefitComposite
{
    public ITest Test { get; }
}

public interface ITest
{
    [Get("/posts/1")]
    Task<string> Test();
}

public class AntiforgeryHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("x-csrf", "1");
        return base.SendAsync(request, cancellationToken);
    }
}