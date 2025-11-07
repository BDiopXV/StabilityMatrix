using System.Text.Json;
using System.Text.Json.Serialization;
using Refit;

namespace StabilityMatrix.Core.Models.Api.ImgBB;

public record ImgBBUploadImageRequest
{
    public string? image { get; set; }

    public string? name { get; set; }

    public int? expiration { get; set; }
}
