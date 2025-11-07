using System.Text.Json.Nodes;
using Refit;
using StabilityMatrix.Core.Models.Api.ImgBB;

namespace StabilityMatrix.Core.Api;

[Headers("User-Agent: StabilityMatrix/1.0")]
public interface IImgBBApi
{
    [Post("/upload")]
    [Headers("Content-Type: application/x-www-form-urlencoded")]
    Task<ImgBBImageUploadResponse> UploadImage(
        [Body(BodySerializationMethod.UrlEncoded)] ImgBBUploadImageRequest request
    );

    [Get("/{id}/{delete_token}")]
    Task<HttpResponseMessage> DeleteImagebyId(int id, [Query] string delete_token);
}
