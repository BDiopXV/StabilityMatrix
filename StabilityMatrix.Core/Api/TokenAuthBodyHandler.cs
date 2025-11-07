using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using NLog;
using Polly;
using Polly.Retry;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Core.Api;

public class TokenAuthBodyHandler : DelegatingHandler
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly AsyncRetryPolicy<HttpResponseMessage> policy;
    private readonly ImgBBAuthTokenProvider tokenProvider;

    private const string AccessTokenField = "key"; // key name to use in POST body

    public Func<HttpRequestMessage, bool> RequestFilter { get; set; } =
        request =>
            request.Method == HttpMethod.Post
            && (request.Content != null)
            && request.RequestUri != null
            && request.RequestUri.AbsolutePath.Contains("/upload", StringComparison.OrdinalIgnoreCase);

    public Func<HttpResponseMessage, bool> ResponseFilter { get; set; } =
        response =>
            (
                response.StatusCode == HttpStatusCode.Unauthorized
                || response.StatusCode == HttpStatusCode.Forbidden
            );

    public TokenAuthBodyHandler(ImgBBAuthTokenProvider tokenProvider)
    {
        this.tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));

        policy = Policy
            .HandleResult(ResponseFilter)
            .RetryAsync(
                async (result, _) =>
                {
                    var oldToken = ObjectHash.GetStringSignature(
                        await tokenProvider.GetAccessTokenAsync().ConfigureAwait(false)
                    );

                    Logger.Info(
                        "Refreshing access token for status ({StatusCode})",
                        result.Result.StatusCode
                    );

                    var newToken = await tokenProvider.RefreshTokensAsync().ConfigureAwait(false);

                    Logger.Info(
                        "Access token refreshed: {OldToken} -> {NewToken}",
                        ObjectHash.GetStringSignature(oldToken),
                        ObjectHash.GetStringSignature(newToken)
                    );
                }
            );
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        return policy.ExecuteAsync(async () =>
        {
            if (RequestFilter(request))
            {
                var accessToken = await tokenProvider.GetAccessTokenAsync().ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    request.Content = await AddOrReplaceTokenInBodyAsync(
                        request.Content,
                        AccessTokenField,
                        accessToken
                    );
                }
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Reads the request body, injects or replaces the "key" form field with the token.
    /// Works for application/x-www-form-urlencoded or multipart/form-data.
    /// </summary>
    private static async Task<HttpContent> AddOrReplaceTokenInBodyAsync(
        HttpContent originalContent,
        string fieldName,
        string token
    )
    {
        if (originalContent is null)
            return originalContent!;

        var contentType = originalContent.Headers.ContentType?.MediaType ?? "";

        if (!contentType.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Warn(
                "TokenAuthBodyHandler only supports application/x-www-form-urlencoded. Skipping token injection."
            );
            return originalContent;
        }

        var body = await originalContent.ReadAsStringAsync().ConfigureAwait(false);
        var dict = System.Web.HttpUtility.ParseQueryString(body);
        dict[fieldName] = token;

        return new StringContent(
            dict.ToString() ?? string.Empty,
            Encoding.UTF8,
            "application/x-www-form-urlencoded"
        );
    }
}
