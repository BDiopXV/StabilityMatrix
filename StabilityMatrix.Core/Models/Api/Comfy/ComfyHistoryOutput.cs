using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public class ComfyHistoryOutput
{
    [JsonPropertyName("images")]
    public List<ComfyImage>? Images { get; set; }

    // Comfy returns videos from nodes like VHS_VideoCombine
    [JsonPropertyName("gifs")]
    public List<ComfyImage>? Videos { get; set; }
}
