using Refit.Composite.Attributes;

namespace Refit.Composite.Sample.Wasm;

[ApiHandler<AntiforgeryHandler>]
public interface ITestComposite : IRefitComposite
{
    public ITest Test { get; }
}

public interface ITest
{
    [Get("/posts/1")]
    Task<IApiResponse<string>> Test();
}

public class AntiforgeryHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("x-csrf", "1");
        return base.SendAsync(request, cancellationToken);
    }
}
