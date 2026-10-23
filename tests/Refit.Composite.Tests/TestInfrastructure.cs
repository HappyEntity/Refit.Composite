using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Refit.Composite.Tests;

/// <summary>
/// Terminal handler that answers every request with "{handlers}|{request uri}",
/// where {handlers} is the comma-separated value of the <see cref="PipelineHeader"/> header.
/// </summary>
public sealed class EchoHandler(HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
{
    public const string PipelineHeader = "X-Pipeline";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var pipeline = request.Headers.TryGetValues(PipelineHeader, out var values) ? string.Join(",", values) : string.Empty;

        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            Content = new StringContent($"{pipeline}|{request.RequestUri}")
        });
    }

    public static Action<IHttpClientBuilder> Install(HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return builder => builder.ConfigurePrimaryHttpMessageHandler(() => new EchoHandler(statusCode));
    }
}

/// <summary>Appends its name to the <see cref="EchoHandler.PipelineHeader"/> header.</summary>
public abstract class TaggingHandler(string tag) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add(EchoHandler.PipelineHeader, tag);
        return base.SendAsync(request, cancellationToken);
    }
}

public sealed class HandlerA() : TaggingHandler("A");

public sealed class HandlerB() : TaggingHandler("B");

public sealed class HandlerC() : TaggingHandler("C");

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<(string Category, LogLevel Level, string Message)> _entries = new();

    public IReadOnlyList<(string Category, LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (_entries) return _entries.ToList();
        }
    }

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger(CapturingLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (provider._entries) provider._entries.Add((category, logLevel, formatter(state, exception)));
        }
    }
}
