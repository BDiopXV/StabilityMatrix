using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Settings;

/// <summary>
/// Settings for LM Studio integration for prompt enhancement and image analysis
/// </summary>
public class LmStudioSettings
{
    /// <summary>
    /// Whether LM Studio prompt enhancement is enabled
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Base URL for the LM Studio API (default: http://localhost:1234)
    /// </summary>
    public string EndpointUrl { get; set; } = "http://localhost:1234";

    /// <summary>
    /// The model to use for text prompt enhancement (if empty, uses the loaded model)
    /// </summary>
    public string? TextModel { get; set; }

    /// <summary>
    /// The model to use for vision/image analysis (if empty, uses the loaded model)
    /// </summary>
    public string? VisionModel { get; set; }

    /// <summary>
    /// System directive/prompt for text prompt enhancement (SFW mode)
    /// Guides how the LLM should enhance or expand prompts
    /// </summary>
    public string TextEnhancementDirective { get; set; } = DefaultTextEnhancementDirective;

    /// <summary>
    /// System directive/prompt for text prompt enhancement (NSFW mode)
    /// Guides how the LLM should enhance or expand prompts without content restrictions
    /// </summary>
    public string TextEnhancementDirectiveNsfw { get; set; } = DefaultTextEnhancementDirectiveNsfw;

    /// <summary>
    /// System directive/prompt for image analysis (SFW mode)
    /// Guides how the LLM should analyze images and generate prompts
    /// </summary>
    public string ImageAnalysisDirective { get; set; } = DefaultImageAnalysisDirective;

    /// <summary>
    /// System directive/prompt for image analysis (NSFW mode)
    /// Guides how the LLM should analyze images and generate prompts without content restrictions
    /// </summary>
    public string ImageAnalysisDirectiveNsfw { get; set; } = DefaultImageAnalysisDirectiveNsfw;

    /// <summary>
    /// System directive/prompt for Wan22 video generation (SFW mode)
    /// Guides how the LLM should generate prompts for video generation
    /// </summary>
    public string VideoGenerationDirective { get; set; } = DefaultVideoGenerationDirective;

    /// <summary>
    /// System directive/prompt for Wan22 video generation (NSFW mode)
    /// Guides how the LLM should generate prompts for video generation without content restrictions
    /// </summary>
    public string VideoGenerationDirectiveNsfw { get; set; } = DefaultVideoGenerationDirectiveNsfw;

    /// <summary>
    /// Temperature for text generation (0.0-2.0, lower = more focused, higher = more creative)
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Maximum tokens to generate in the response
    /// </summary>
    public int MaxTokens { get; set; } = 500;

    /// <summary>
    /// Timeout in seconds for API requests
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to automatically enhance prompts before generation
    /// </summary>
    public bool AutoEnhancePrompts { get; set; }

    /// <summary>
    /// Default system directive for text prompt enhancement (SFW)
    /// </summary>
    public const string DefaultTextEnhancementDirective = """
        You are an expert prompt engineer for Stable Diffusion and image generation AI models.
        Your task is to enhance and expand the user's prompt to create more detailed, descriptive, and effective prompts for image generation.

        Guidelines:
        - Maintain the original intent and subject matter
        - Add relevant artistic details (lighting, composition, style, atmosphere)
        - Include quality enhancing terms appropriate for the model
        - Keep the prompt coherent and well-structured
        - Use comma-separated tags/phrases common in image generation
        - Do not add NSFW or inappropriate content unless explicitly requested
        - Output ONLY the enhanced prompt, no explanations or additional text
        """;

    /// <summary>
    /// Default system directive for text prompt enhancement (NSFW)
    /// </summary>
    public const string DefaultTextEnhancementDirectiveNsfw = """
        You are an expert prompt engineer for Stable Diffusion and image generation AI models.
        Your task is to enhance and expand the user's prompt to create more detailed, descriptive, and effective prompts for image generation.

        Guidelines:
        - Maintain the original intent and subject matter
        - Add relevant artistic details (lighting, composition, style, atmosphere)
        - Include quality enhancing terms appropriate for the model
        - Keep the prompt coherent and well-structured
        - Use comma-separated tags/phrases common in image generation
        - Adult/NSFW content is allowed and should be enhanced appropriately when requested
        - Output ONLY the enhanced prompt, no explanations or additional text
        """;

    /// <summary>
    /// Default system directive for image analysis (SFW)
    /// </summary>
    public const string DefaultImageAnalysisDirective = """
        You are an expert at analyzing images and creating detailed prompts for image generation AI models.
        Analyze the provided image and generate a comprehensive prompt that could recreate it.

        Guidelines:
        - Describe the main subject(s) and their characteristics
        - Note the art style, medium, and technique if apparent
        - Include lighting, color palette, and atmosphere
        - Mention composition and perspective
        - Add relevant quality and style tags
        - Use comma-separated tags/phrases common in image generation
        - Be detailed but concise
        - Output ONLY the prompt, no explanations or additional text
        """;

    /// <summary>
    /// Default system directive for image analysis (NSFW)
    /// </summary>
    public const string DefaultImageAnalysisDirectiveNsfw = """
        You are an expert at analyzing images and creating detailed prompts for image generation AI models.
        Analyze the provided image and generate a comprehensive prompt that could recreate it.

        Guidelines:
        - Describe the main subject(s) and their characteristics in full detail
        - Note the art style, medium, and technique if apparent
        - Include lighting, color palette, and atmosphere
        - Mention composition and perspective
        - Add relevant quality and style tags
        - Use comma-separated tags/phrases common in image generation
        - Be detailed but concise
        - Adult/NSFW content should be described accurately when present
        - Output ONLY the prompt, no explanations or additional text
        """;

    /// <summary>
    /// Default system directive for Wan22 video generation (SFW)
    /// </summary>
    public const string DefaultVideoGenerationDirective = """
        You are an expert prompt engineer for AI video generation models like Wan2.1.
        Your task is to create detailed, descriptive prompts optimized for video generation.

        Guidelines:
        - Focus on describing motion, action, and temporal changes
        - Describe camera movements (pan, zoom, tracking, static)
        - Include subject movements and animations
        - Note transitions and scene dynamics
        - Describe the atmosphere and mood throughout the video
        - Include lighting changes if relevant
        - Use descriptive language for smooth, coherent video generation
        - Keep descriptions focused on achievable, realistic motions
        - Do not add NSFW or inappropriate content
        - Output ONLY the video prompt, no explanations or additional text
        """;

    /// <summary>
    /// Default system directive for Wan22 video generation (NSFW)
    /// </summary>
    public const string DefaultVideoGenerationDirectiveNsfw = """
        You are an expert prompt engineer for AI video generation models like Wan2.1.
        Your task is to create detailed, descriptive prompts optimized for video generation.

        Guidelines:
        - Focus on describing motion, action, and temporal changes
        - Describe camera movements (pan, zoom, tracking, static)
        - Include subject movements and animations
        - Note transitions and scene dynamics
        - Describe the atmosphere and mood throughout the video
        - Include lighting changes if relevant
        - Use descriptive language for smooth, coherent video generation
        - Keep descriptions focused on achievable, realistic motions
        - Adult/NSFW content is allowed and should be described appropriately when requested
        - Output ONLY the video prompt, no explanations or additional text
        """;
}
