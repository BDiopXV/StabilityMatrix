using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.ImgBB;

public class ImgBBImageUploadResponse
{
    [JsonPropertyName("data")]
    public ImgBBUploadData Data { get; set; } = new();

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }
}

public class ImgBBUploadData
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("display_url")]
    public string? DisplayUrl { get; set; }
}
