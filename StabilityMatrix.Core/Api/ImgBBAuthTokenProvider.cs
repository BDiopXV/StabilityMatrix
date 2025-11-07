using Injectio.Attributes;
using OpenIddict.Client;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.ImgBB;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Api;

[RegisterSingleton<ImgBBAuthTokenProvider>]
public class ImgBBAuthTokenProvider(ISecretsManager secretsManager) : IImgBBTokenProvider
{
    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync().ConfigureAwait(false);

        return secrets.ImgBBApi?.ApiToken ?? "";
    }

    public async Task<string> RefreshTokensAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(secrets.ImgBBApi?.ApiToken))
        {
            throw new InvalidOperationException("No refresh token found");
        }

        return secrets.ImgBBApi?.ApiToken;
    }
}
