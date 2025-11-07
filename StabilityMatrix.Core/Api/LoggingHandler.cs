using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StabilityMatrix.Core.Api;

/// <summary>
/// Logs outgoing HTTP requests and responses for Refit clients.
/// </summary>
public sealed class LoggingHandler : DelegatingHandler
{
    private readonly ILogger<LoggingHandler> _logger;

    public LoggingHandler(ILogger<LoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("→ {Method} {Url}", request.Method, request.RequestUri);

        if (request.Content != null)
        {
            var content = await request.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(content))
                _logger.LogTrace(
                    "Request body ({Length} chars): {Preview}",
                    content.Length,
                    content[..Math.Min(content.Length, 500)]
                );
        }

        var response = await base.SendAsync(request, cancellationToken);
        sw.Stop();

        _logger.LogInformation("← {Status} ({Elapsed} ms)", (int)response.StatusCode, sw.ElapsedMilliseconds);

        var respText = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrEmpty(respText))
            _logger.LogTrace(
                "Response body ({Length} chars): {Preview}",
                respText.Length,
                respText[..Math.Min(respText.Length, 500)]
            );

        return response;
    }
}
