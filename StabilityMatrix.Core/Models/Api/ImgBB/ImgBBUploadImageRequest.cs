using System.Text.Json;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.ImgBB;

public record ImgBBUploadImageRequest : IFormattable
{
    [JsonPropertyName("key")]
    public required string ApiKey { get; set; }

    [JsonPropertyName("image")]
    public string b64_image { get; set; } = "";

    [JsonPropertyName("name")]
    public string image_name { get; set; } = "";

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return JsonSerializer.Serialize(new { json = this });
    }
}
