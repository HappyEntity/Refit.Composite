using System.Net;

namespace Refit.Composite.Handlers;

/// <summary>
/// Catches <see cref="HttpRequestException"/> and transforms it into a 503 Service Unavailable response.
/// Can be excluded using <see cref="Attributes.ApiIgnoreHandlerAttribute{THandler}"/>.
/// </summary>
public class ErrorHandlingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent(exception.Message),
                RequestMessage = request
            };
        }
    }
}