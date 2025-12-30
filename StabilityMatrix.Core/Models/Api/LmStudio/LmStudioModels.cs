using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.LmStudio;

/// <summary>
/// Request model for LM Studio chat completions (OpenAI-compatible)
/// </summary>
public class LmStudioChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("messages")]
    public List<LmStudioChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public double? FrequencyPenalty { get; set; }

    [JsonPropertyName("presence_penalty")]
    public double? PresencePenalty { get; set; }
}

/// <summary>
/// Chat message with support for text and image content
/// </summary>
public class LmStudioChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    /// <summary>
    /// Content can be a string or an array of content parts for multimodal messages
    /// </summary>
    [JsonPropertyName("content")]
    public object Content { get; set; } = string.Empty;
}

/// <summary>
/// Content part for multimodal messages
/// </summary>
[JsonDerivedType(typeof(LmStudioTextContent), "text")]
[JsonDerivedType(typeof(LmStudioImageContent), "image_url")]
public abstract class LmStudioContentPart
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

/// <summary>
/// Text content part
/// </summary>
public class LmStudioTextContent : LmStudioContentPart
{
    [JsonPropertyName("type")]
    public override string Type => "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Image content part for vision models
/// </summary>
public class LmStudioImageContent : LmStudioContentPart
{
    [JsonPropertyName("type")]
    public override string Type => "image_url";

    [JsonPropertyName("image_url")]
    public LmStudioImageUrl ImageUrl { get; set; } = new();
}

/// <summary>
/// Image URL object (supports data URIs for base64 images)
/// </summary>
public class LmStudioImageUrl
{
    /// <summary>
    /// URL or data URI (e.g., "data:image/png;base64,...")
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Optional detail level: "auto", "low", or "high"
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}

/// <summary>
/// Response from LM Studio chat completions
/// </summary>
public class LmStudioChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("created")]
    public long? Created { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public List<LmStudioChatChoice> Choices { get; set; } = [];

    [JsonPropertyName("usage")]
    public LmStudioUsage? Usage { get; set; }
}

/// <summary>
/// Chat completion choice
/// </summary>
public class LmStudioChatChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public LmStudioChatMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// Token usage statistics
/// </summary>
public class LmStudioUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

/// <summary>
/// Response from LM Studio models endpoint
/// </summary>
public class LmStudioModelsResponse
{
    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("data")]
    public List<LmStudioModel> Data { get; set; } = [];
}

/// <summary>
/// Model information
/// </summary>
public class LmStudioModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("owned_by")]
    public string? OwnedBy { get; set; }
}
