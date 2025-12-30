using StabilityMatrix.Core.Models.Api.LmStudio;

namespace StabilityMatrix.Core.Services;

/// <summary>
/// Service for interacting with LM Studio for prompt enhancement and image analysis
/// </summary>
public interface ILmStudioService
{
    /// <summary>
    /// Gets whether the LM Studio service is enabled in settings
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Tests the connection to LM Studio
    /// </summary>
    /// <returns>True if connected successfully, false otherwise</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of available models from LM Studio
    /// </summary>
    Task<IReadOnlyList<LmStudioModel>> GetModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enhances a text prompt using LM Studio
    /// </summary>
    /// <param name="prompt">The original prompt to enhance</param>
    /// <param name="negativePrompt">Optional negative prompt for context</param>
    /// <param name="loraTriggerWords">Optional LoRA trigger words that must be included in the enhanced prompt</param>
    /// <param name="customDirective">Optional custom directive to override the default</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The enhanced prompt</returns>
    Task<LmStudioPromptResult> EnhancePromptAsync(
        string prompt,
        string? negativePrompt = null,
        IEnumerable<string>? loraTriggerWords = null,
        string? customDirective = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Analyzes an image and generates a descriptive prompt
    /// </summary>
    /// <param name="imageData">Base64 encoded image data or image bytes</param>
    /// <param name="mimeType">The MIME type of the image (e.g., "image/png", "image/jpeg")</param>
    /// <param name="additionalContext">Optional additional context or instructions</param>
    /// <param name="customDirective">Optional custom directive to override the default</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated prompt based on the image</returns>
    Task<LmStudioPromptResult> AnalyzeImageAsync(
        byte[] imageData,
        string mimeType = "image/png",
        string? additionalContext = null,
        string? customDirective = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Analyzes an image from a file path and generates a descriptive prompt
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="additionalContext">Optional additional context or instructions</param>
    /// <param name="customDirective">Optional custom directive to override the default</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated prompt based on the image</returns>
    Task<LmStudioPromptResult> AnalyzeImageFromFileAsync(
        string imagePath,
        string? additionalContext = null,
        string? customDirective = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Simple convenience method to enhance a prompt and return just the text
    /// </summary>
    /// <param name="prompt">The prompt to enhance</param>
    /// <param name="negativePrompt">Optional negative prompt for context to avoid contradictions</param>
    /// <param name="loraTriggerWords">Optional LoRA trigger words that must be included in the enhanced prompt</param>
    /// <param name="useNsfwMode">Whether to use NSFW directive</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The enhanced prompt text</returns>
    Task<string> EnhancePromptSimpleAsync(
        string prompt,
        string? negativePrompt = null,
        IEnumerable<string>? loraTriggerWords = null,
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Simple convenience method to analyze an image and return just the prompt text
    /// </summary>
    /// <param name="imageData">The image bytes</param>
    /// <param name="useNsfwMode">Whether to use NSFW directive</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated prompt text</returns>
    Task<string> AnalyzeImageSimpleAsync(
        byte[] imageData,
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Enhances a prompt using both the existing prompt and an image for context
    /// </summary>
    /// <param name="prompt">The existing prompt to enhance</param>
    /// <param name="imageData">The image bytes for additional context</param>
    /// <param name="negativePrompt">Optional negative prompt for context to avoid contradictions</param>
    /// <param name="loraTriggerWords">Optional LoRA trigger words that must be included in the enhanced prompt</param>
    /// <param name="useNsfwMode">Whether to use NSFW directive</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The enhanced prompt text</returns>
    Task<string> EnhancePromptWithImageAsync(
        string prompt,
        byte[] imageData,
        string? negativePrompt = null,
        IEnumerable<string>? loraTriggerWords = null,
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Generates a creative prompt without any input
    /// </summary>
    /// <param name="useNsfwMode">Whether to use NSFW directive</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A creative prompt text</returns>
    Task<string> GenerateCreativePromptAsync(
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Generates a creative prompt based on provided tags as context/inspiration
    /// </summary>
    /// <param name="tagsContext">Comma-separated list of tags to use as inspiration</param>
    /// <param name="useNsfwMode">Whether to use NSFW directive</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A creative prompt text inspired by the tags</returns>
    Task<string> GeneratePromptFromTagsAsync(
        string tagsContext,
        IEnumerable<string>? loraTriggerWords = null,
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Generates a video prompt based on provided tags as context/inspiration (for Wan22)
    /// </summary>
    /// <param name="tagsContext">Comma-separated list of tags to use as inspiration</param>
    /// <param name="useNsfwMode">Whether to use NSFW directive</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A video prompt text inspired by the tags</returns>
    Task<string> GenerateVideoPromptFromTagsAsync(
        string tagsContext,
        IEnumerable<string>? loraTriggerWords = null,
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Enhances a video prompt (for Wan22)
    /// </summary>
    /// <param name="prompt">The prompt to enhance</param>
    /// <param name="negativePrompt">Optional negative prompt for context to avoid contradictions</param>
    /// <param name="loraTriggerWords">Optional LoRA trigger words that must be included in the enhanced prompt</param>
    /// <param name="useNsfwMode">Whether to use NSFW directive</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The enhanced video prompt</returns>
    Task<string> EnhanceVideoPromptAsync(
        string prompt,
        string? negativePrompt = null,
        IEnumerable<string>? loraTriggerWords = null,
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Analyzes an image and generates a video prompt (for Wan22 image-to-video)
    /// </summary>
    /// <param name="imageData">The image bytes</param>
    /// <param name="existingPrompt">Optional existing prompt to enhance</param>
    /// <param name="negativePrompt">Optional negative prompt for context to avoid contradictions</param>
    /// <param name="loraTriggerWords">Optional LoRA trigger words that must be included in the enhanced prompt</param>
    /// <param name="useNsfwMode">Whether to use NSFW directive</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A video prompt based on the image</returns>
    Task<string> GenerateVideoPromptFromImageAsync(
        byte[] imageData,
        string? existingPrompt = null,
        string? negativePrompt = null,
        IEnumerable<string>? loraTriggerWords = null,
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Unloads all currently loaded models from LM Studio to free up VRAM.
    /// Uses the "lms unload" command.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if unload was successful, false otherwise</returns>
    Task<bool> UnloadModelsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from LM Studio prompt operations
/// </summary>
public class LmStudioPromptResult
{
    /// <summary>
    /// The generated or enhanced prompt
    /// </summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// Optional enhanced negative prompt (if provided in the response)
    /// </summary>
    public string? NegativePrompt { get; init; }

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The model used for generation
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Token usage information
    /// </summary>
    public LmStudioUsage? Usage { get; init; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static LmStudioPromptResult Success(
        string prompt,
        string? negativePrompt = null,
        string? model = null,
        LmStudioUsage? usage = null
    ) =>
        new()
        {
            Prompt = prompt,
            NegativePrompt = negativePrompt,
            IsSuccess = true,
            Model = model,
            Usage = usage,
        };

    /// <summary>
    /// Creates a failure result
    /// </summary>
    public static LmStudioPromptResult Failure(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };
}
