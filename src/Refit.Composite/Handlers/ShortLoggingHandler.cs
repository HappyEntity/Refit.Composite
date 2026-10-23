using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Refit.Composite.Handlers;

/// <summary>
/// Provides lightweight, status-code-aware HTTP logging.
/// Only the scheme, host and path of the request URI are logged: the query string is omitted because it often carries secrets.
/// Can be excluded using <see cref="Attributes.ApiIgnoreHandlerAttribute{THandler}"/>.
/// </summary>
public class ShortLoggingHandler(ILogger<ShortLoggingHandler> logger) : DelegatingHandler
{
    private const string ResponseTemplate = "HTTP {Method} {Uri} responded {StatusCode} in {Elapsed:F2} ms";

    private static readonly Action<ILogger, HttpMethod, string?, int, double, Exception?> LogResponseInformation =
        LoggerMessage.Define<HttpMethod, string?, int, double>(LogLevel.Information, new EventId(1, "HttpResponse"), ResponseTemplate);

    private static readonly Action<ILogger, HttpMethod, string?, int, double, Exception?> LogResponseWarning =
        LoggerMessage.Define<HttpMethod, string?, int, double>(LogLevel.Warning, new EventId(1, "HttpResponse"), ResponseTemplate);

    private static readonly Action<ILogger, HttpMethod, string?, int, double, Exception?> LogResponseError =
        LoggerMessage.Define<HttpMethod, string?, int, double>(LogLevel.Error, new EventId(1, "HttpResponse"), ResponseTemplate);

    private static readonly Action<ILogger, HttpMethod, string?, double, Exception?> LogCanceled =
        LoggerMessage.Define<HttpMethod, string?, double>(LogLevel.Warning, new EventId(2, "HttpCanceled"),
            "HTTP {Method} {Uri} was canceled after {Elapsed:F2} ms");

    private static readonly Action<ILogger, HttpMethod, string?, double, Exception?> LogFailed =
        LoggerMessage.Define<HttpMethod, string?, double>(LogLevel.Error, new EventId(3, "HttpFailed"),
            "HTTP {Method} {Uri} failed after {Elapsed:F2} ms");

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();

        HttpResponseMessage response;
        try {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) {
            LogCanceled(logger, request.Method, FormatUri(request.RequestUri), GetElapsedMilliseconds(start), exception);
            throw;
        }
        catch (Exception exception) {
            LogFailed(logger, request.Method, FormatUri(request.RequestUri), GetElapsedMilliseconds(start), exception);
            throw;
        }

        var log = response.StatusCode switch
        {
            >= HttpStatusCode.InternalServerError => LogResponseError,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => LogResponseWarning,
            _ => LogResponseInformation
        };

        log(logger, request.Method, FormatUri(request.RequestUri), (int)response.StatusCode, GetElapsedMilliseconds(start), null);

        return response;
    }

    private static string? FormatUri(Uri? uri)
    {
        if (uri is null) return null;

        return uri.IsAbsoluteUri
            ? uri.GetLeftPart(UriPartial.Path)
            : uri.OriginalString.Split('?')[0];
    }

    private static double GetElapsedMilliseconds(long start)
    {
        return (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
    }
}
