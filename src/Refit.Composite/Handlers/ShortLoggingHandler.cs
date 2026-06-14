using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Refit.Composite.Handlers;

/// <summary>
/// Provides lightweight, status-code-aware HTTP logging.
/// Can be excluded using <see cref="Attributes.ApiIgnoreHandlerAttribute{THandler}"/>.
/// </summary>
public class ShortLoggingHandler(ILogger<ShortLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var startTime = Stopwatch.GetTimestamp();
        var response = await base.SendAsync(request, ct);
        var elapsedMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;

        var level = response.StatusCode switch
        {
            >= HttpStatusCode.InternalServerError => LogLevel.Error,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => LogLevel.Warning,
            _ => LogLevel.Information
        };

        logger.Log(level, "HTTP {Method} {Uri} responded {StatusCode} in {Elapsed:F2} ms",
            request.Method, request.RequestUri, (int)response.StatusCode, elapsedMs);

        return response;
    }
}
