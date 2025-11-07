using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Refit;

namespace StabilityMatrix.Core.Api;

/// <summary>
/// Refit interface for the GET /intent/post endpoint (web intent).
/// BaseAddress should be "https://civitai.com"
/// This call constructs a query-string based intent (mediaUrl, title, description, tags, detailsUrl).
/// Note: Typically this URI is opened in a browser so the user can finish the post flow.
/// </summary>
public interface ICivitIntentApi
{
    /// <summary>
    /// Build the intent using query parameters and perform a GET to /intent/post.
    /// Refit will serialize the request object's properties to query parameters when annotated with [Query].
    /// </summary>
    [Get("/intent/post")]
    Task<CivitAIIntentGetResponse> GetIntentAsync([Query] CivitAIIntentGetRequest request);

    /// <summary>
    /// Alternative overload accepting explicit parameters (useful if you prefer to pass primitives).
    /// </summary>
    [Get("/intent/post")]
    Task<CivitAIIntentGetResponse> GetIntentAsync(
        [AliasAs("mediaUrl")] string mediaUrl,
        [AliasAs("title")] string? title = null,
        [AliasAs("description")] string? description = null,
        [AliasAs("tags")] string? tags = null,
        [AliasAs("detailsUrl")] string? detailsUrl = null
    );
}

// Request model used for query serialization
public class CivitAIIntentGetRequest
{
    [AliasAs("mediaUrl")]
    [JsonPropertyName("mediaUrl")]
    public string MediaUrl { get; set; } = "";

    [AliasAs("title")]
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [AliasAs("description")]
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    // Comma-separated list of up to 5 tags
    [AliasAs("tags")]
    [JsonPropertyName("tags")]
    public string? Tags { get; set; }

    // Optional details URL (must be an allowed/approved domain for Civitai)
    [AliasAs("detailsUrl")]
    [JsonPropertyName("detailsUrl")]
    public string? DetailsUrl { get; set; }
}

// Minimal response model. The actual endpoint often returns HTML/redirect for browser use.
// Use ApiResponse<T> to inspect status and raw content if needed.
public class CivitAIIntentGetResponse
{
    [JsonPropertyName("success")]
    public bool? Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    // If the endpoint returns additional structured JSON, map fields here.
}

// DTO for the detailsUrl response that your service should provide
public class CivitAIIntentDetailsResponse
{
    [JsonPropertyName("source")]
    public CivitAIIntentSource? Source { get; set; }

    [JsonPropertyName("createUrl")]
    public string? CreateUrl { get; set; }

    [JsonPropertyName("referenceUrl")]
    public string? ReferenceUrl { get; set; }

    // free-form key/value details
    [JsonPropertyName("details")]
    public Dictionary<string, JsonElementWrapper>? Details { get; set; }
}

public class CivitAIIntentSource
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }
}

// Helper wrapper that can hold string/number/boolean (use System.Text.Json.JsonElement in consumer code)
public class JsonElementWrapper
{
    [JsonPropertyName("value")]
    public object? Value { get; set; }
}
