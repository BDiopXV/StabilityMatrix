using System.ComponentModel;
using Refit;
using StabilityMatrix.Core.Models.Api.LmStudio;

namespace StabilityMatrix.Core.Api;

/// <summary>
/// API interface for LM Studio's OpenAI-compatible API
/// </summary>
[Localizable(false)]
[Headers("User-Agent: StabilityMatrix")]
public interface ILmStudioApi
{
    /// <summary>
    /// Get list of available models
    /// </summary>
    [Get("/v1/models")]
    Task<LmStudioModelsResponse> GetModels(CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a chat completion
    /// </summary>
    [Post("/v1/chat/completions")]
    Task<LmStudioChatCompletionResponse> CreateChatCompletion(
        [Body] LmStudioChatCompletionRequest request,
        CancellationToken cancellationToken = default
    );
}
