using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.ImgBB;

public record ImgBBImageUploadResponse : IFormattable
{
    [JsonPropertyName("result")]
    public required JsonObject Result { get; init; }

    public string? ApiKey => Result["data"]?["url"]?.GetValue<string>();
    public string? Id => Result["data"]?["id"]?.GetValue<string>();

    public string? delete_url => Result["data"]?["delete_url"]?.GetValue<string>();

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return JsonSerializer.Serialize(new { json = this });
    }
}
