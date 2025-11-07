namespace StabilityMatrix.Core.Api;

public interface IImgBBTokenProvider
{
    Task<string> GetAccessTokenAsync();
    Task<string> RefreshTokensAsync();
}
