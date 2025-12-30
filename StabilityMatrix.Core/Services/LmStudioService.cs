using System.Net.Http.Json;
using System.Text.Json;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using Refit;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.LmStudio;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Core.Services;

/// <summary>
/// Service for interacting with LM Studio for prompt enhancement and image analysis
/// </summary>
[RegisterSingleton<ILmStudioService, LmStudioService>]
public class LmStudioService : ILmStudioService
{
    private readonly ILogger<LmStudioService> logger;
    private readonly ISettingsManager settingsManager;
    private readonly IHttpClientFactory httpClientFactory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public LmStudioService(
        ILogger<LmStudioService> logger,
        ISettingsManager settingsManager,
        IHttpClientFactory httpClientFactory
    )
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.httpClientFactory = httpClientFactory;
    }

    private LmStudioSettings Settings => settingsManager.Settings.LmStudioSettings ?? new LmStudioSettings();

    /// <inheritdoc />
    public bool IsEnabled => Settings.IsEnabled;

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await GetModelsAsync(cancellationToken);
            return models.Count > 0;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to connect to LM Studio at {Url}", Settings.EndpointUrl);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LmStudioModel>> GetModelsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var client = CreateHttpClient();
            var response = await client.GetFromJsonAsync<LmStudioModelsResponse>(
                "/v1/models",
                JsonOptions,
                cancellationToken
            );
            return response?.Data ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get models from LM Studio");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<LmStudioPromptResult> EnhancePromptAsync(
        string prompt,
        string? negativePrompt = null,
        IEnumerable<string>? loraTriggerWords = null,
        string? customDirective = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsEnabled)
        {
            return LmStudioPromptResult.Failure("LM Studio integration is not enabled");
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return LmStudioPromptResult.Failure("Prompt cannot be empty");
        }

        try
        {
            var directive = customDirective ?? Settings.TextEnhancementDirective;
            var userMessage = BuildEnhancementUserMessage(prompt, negativePrompt, loraTriggerWords);

            var request = new LmStudioChatCompletionRequest
            {
                Model = Settings.TextModel,
                Messages =
                [
                    new LmStudioChatMessage { Role = "system", Content = directive },
                    new LmStudioChatMessage { Role = "user", Content = userMessage },
                ],
                Temperature = Settings.Temperature,
                MaxTokens = Settings.MaxTokens,
                Stream = false,
            };

            var response = await SendChatCompletionAsync(request, cancellationToken);

            if (response?.Choices is not { Count: > 0 })
            {
                return LmStudioPromptResult.Failure("No response received from LM Studio");
            }

            var content = response.Choices[0].Message?.Content?.ToString() ?? string.Empty;
            var (enhancedPrompt, enhancedNegative) = ParsePromptResponse(content);

            return LmStudioPromptResult.Success(
                enhancedPrompt,
                enhancedNegative,
                response.Model,
                response.Usage
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enhance prompt via LM Studio");
            return LmStudioPromptResult.Failure($"Failed to enhance prompt: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<LmStudioPromptResult> AnalyzeImageAsync(
        byte[] imageData,
        string mimeType = "image/png",
        string? additionalContext = null,
        string? customDirective = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsEnabled)
        {
            return LmStudioPromptResult.Failure("LM Studio integration is not enabled");
        }

        if (imageData.Length == 0)
        {
            return LmStudioPromptResult.Failure("Image data cannot be empty");
        }

        try
        {
            var directive = customDirective ?? Settings.ImageAnalysisDirective;
            var base64Image = Convert.ToBase64String(imageData);
            var dataUri = $"data:{mimeType};base64,{base64Image}";

            var contentParts = new List<object>
            {
                new LmStudioImageContent
                {
                    ImageUrl = new LmStudioImageUrl { Url = dataUri, Detail = "high" },
                },
            };

            var textPrompt = "Analyze this image and generate a detailed prompt for recreating it.";
            if (!string.IsNullOrWhiteSpace(additionalContext))
            {
                textPrompt += $"\n\nAdditional context: {additionalContext}";
            }

            contentParts.Add(new LmStudioTextContent { Text = textPrompt });

            var request = new LmStudioChatCompletionRequest
            {
                Model = Settings.VisionModel ?? Settings.TextModel,
                Messages =
                [
                    new LmStudioChatMessage { Role = "system", Content = directive },
                    new LmStudioChatMessage { Role = "user", Content = contentParts },
                ],
                Temperature = Settings.Temperature,
                MaxTokens = Settings.MaxTokens,
                Stream = false,
            };

            var response = await SendChatCompletionAsync(request, cancellationToken);

            if (response?.Choices is not { Count: > 0 })
            {
                return LmStudioPromptResult.Failure("No response received from LM Studio");
            }

            var content = response.Choices[0].Message?.Content?.ToString() ?? string.Empty;
            var (generatedPrompt, generatedNegative) = ParsePromptResponse(content);

            return LmStudioPromptResult.Success(
                generatedPrompt,
                generatedNegative,
                response.Model,
                response.Usage
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze image via LM Studio");
            return LmStudioPromptResult.Failure($"Failed to analyze image: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<LmStudioPromptResult> AnalyzeImageFromFileAsync(
        string imagePath,
        string? additionalContext = null,
        string? customDirective = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(imagePath))
        {
            return LmStudioPromptResult.Failure($"Image file not found: {imagePath}");
        }

        var imageData = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        var mimeType = GetMimeType(imagePath);

        return await AnalyzeImageAsync(
            imageData,
            mimeType,
            additionalContext,
            customDirective,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<string> EnhancePromptSimpleAsync(
        string prompt,
        string? negativePrompt = null,
        IEnumerable<string>? loraTriggerWords = null,
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    )
    {
        var directive = useNsfwMode
            ? Settings.TextEnhancementDirectiveNsfw
            : Settings.TextEnhancementDirective;
        var result = await EnhancePromptAsync(
            prompt,
            negativePrompt,
            loraTriggerWords,
            directive,
            cancellationToken
        );
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Failed to enhance prompt");
        }
        return result.Prompt;
    }

    /// <inheritdoc />
    public async Task<string> AnalyzeImageSimpleAsync(
        byte[] imageData,
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    )
    {
        var directive = useNsfwMode ? Settings.ImageAnalysisDirectiveNsfw : Settings.ImageAnalysisDirective;
        var result = await AnalyzeImageAsync(imageData, "image/png", null, directive, cancellationToken);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Failed to analyze image");
        }
        return result.Prompt;
    }

    /// <inheritdoc />
    public async Task<string> EnhancePromptWithImageAsync(
        string prompt,
        byte[] imageData,
        string? negativePrompt = null,
        IEnumerable<string>? loraTriggerWords = null,
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    )
    {
        var directive = useNsfwMode ? Settings.ImageAnalysisDirectiveNsfw : Settings.ImageAnalysisDirective;

        var additionalContext =
            $"The user wants to enhance this existing prompt based on the image: {prompt}";

        // Add LoRA trigger words requirement
        var triggerWordsList = loraTriggerWords?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (triggerWordsList is { Count: > 0 })
        {
            var triggerWordsText = string.Join(", ", triggerWordsList);
            additionalContext +=
                $"\n\nREQUIRED LORA TRIGGER WORDS (MUST include these in your response): {triggerWordsText}\n\nIMPORTANT: Your enhanced prompt MUST include ALL of the above trigger words. These are required to activate LoRA models. Integrate them naturally into the prompt.";
        }

        if (!string.IsNullOrWhiteSpace(negativePrompt))
        {
            additionalContext +=
                $"\n\nNEGATIVE PROMPT (DO NOT include these elements): {negativePrompt}\n\nCRITICAL: Your response must NOT contain ANY words or concepts from the negative prompt. Only describe what SHOULD appear.";
        }

        var result = await AnalyzeImageAsync(
            imageData,
            "image/png",
            additionalContext,
            directive,
            cancellationToken
        );
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Failed to enhance prompt with image");
        }
        return result.Prompt;
    }

    /// <inheritdoc />
    public async Task<string> GenerateCreativePromptAsync(
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("LM Studio integration is not enabled");
        }

        try
        {
            var directive = useNsfwMode
                ? Settings.TextEnhancementDirectiveNsfw
                : Settings.TextEnhancementDirective;

            var request = new LmStudioChatCompletionRequest
            {
                Model = Settings.TextModel,
                Messages =
                [
                    new LmStudioChatMessage { Role = "system", Content = directive },
                    new LmStudioChatMessage
                    {
                        Role = "user",
                        Content =
                            "Generate a creative and detailed prompt for image generation. Be imaginative and descriptive.",
                    },
                ],
                Temperature = Math.Max(Settings.Temperature, 0.8), // Use higher temperature for creativity
                MaxTokens = Settings.MaxTokens,
                Stream = false,
            };

            var response = await SendChatCompletionAsync(request, cancellationToken);

            if (response?.Choices is not { Count: > 0 })
            {
                throw new InvalidOperationException("No response received from LM Studio");
            }

            var content = response.Choices[0].Message?.Content?.ToString() ?? string.Empty;
            var (generatedPrompt, _) = ParsePromptResponse(content);
            return generatedPrompt;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate creative prompt via LM Studio");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GeneratePromptFromTagsAsync(
        string tagsContext,
        IEnumerable<string>? loraTriggerWords = null,
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("LM Studio integration is not enabled");
        }

        try
        {
            var directive = useNsfwMode
                ? Settings.TextEnhancementDirectiveNsfw
                : Settings.TextEnhancementDirective;

            var userMessage = $"""
                I have the following tags as inspiration: {tagsContext}

                Using these tags as creative inspiration, generate a detailed and coherent prompt for image generation.
                Don't just list the tags - create a flowing, descriptive prompt that incorporates the themes and concepts from these tags.
                Be creative and descriptive, adding details about composition, lighting, atmosphere, and style where appropriate.
                Output only the final prompt, nothing else.
                """;

            // Add LoRA trigger words requirement
            var triggerWordsList = loraTriggerWords?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (triggerWordsList is { Count: > 0 })
            {
                var triggerWordsText = string.Join(", ", triggerWordsList);
                userMessage +=
                    $"\n\nREQUIRED LORA TRIGGER WORDS (MUST include these in your response): {triggerWordsText}\n\nIMPORTANT: Your generated prompt MUST include ALL of the above trigger words. Integrate them naturally into the prompt.";
            }

            var request = new LmStudioChatCompletionRequest
            {
                Model = Settings.TextModel,
                Messages =
                [
                    new LmStudioChatMessage { Role = "system", Content = directive },
                    new LmStudioChatMessage { Role = "user", Content = userMessage },
                ],
                Temperature = Math.Max(Settings.Temperature, 0.8), // Use higher temperature for creativity
                MaxTokens = Settings.MaxTokens,
                Stream = false,
            };

            var response = await SendChatCompletionAsync(request, cancellationToken);

            if (response?.Choices is not { Count: > 0 })
            {
                throw new InvalidOperationException("No response received from LM Studio");
            }

            var content = response.Choices[0].Message?.Content?.ToString() ?? string.Empty;
            var (generatedPrompt, _) = ParsePromptResponse(content);
            return generatedPrompt;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate prompt from tags via LM Studio");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GenerateVideoPromptFromTagsAsync(
        string tagsContext,
        IEnumerable<string>? loraTriggerWords = null,
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("LM Studio integration is not enabled");
        }

        try
        {
            var directive = useNsfwMode
                ? Settings.VideoGenerationDirectiveNsfw
                : Settings.VideoGenerationDirective;

            var userMessage = $"""
                I have the following tags as inspiration: {tagsContext}

                Using these tags as creative inspiration, generate a detailed video prompt optimized for AI video generation.
                Focus on describing motion, camera movement, and temporal changes that would make an engaging video.
                Don't just list the tags - create a flowing, descriptive prompt that incorporates the themes from these tags.
                Output only the final video prompt, nothing else.
                """;

            // Add LoRA trigger words requirement
            var triggerWordsList = loraTriggerWords?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (triggerWordsList is { Count: > 0 })
            {
                var triggerWordsText = string.Join(", ", triggerWordsList);
                userMessage +=
                    $"\n\nREQUIRED LORA TRIGGER WORDS (MUST include these in your response): {triggerWordsText}\n\nIMPORTANT: Your generated prompt MUST include ALL of the above trigger words. Integrate them naturally into the prompt.";
            }

            var request = new LmStudioChatCompletionRequest
            {
                Model = Settings.TextModel,
                Messages =
                [
                    new LmStudioChatMessage { Role = "system", Content = directive },
                    new LmStudioChatMessage { Role = "user", Content = userMessage },
                ],
                Temperature = Math.Max(Settings.Temperature, 0.8),
                MaxTokens = Settings.MaxTokens,
                Stream = false,
            };

            var response = await SendChatCompletionAsync(request, cancellationToken);

            if (response?.Choices is not { Count: > 0 })
            {
                throw new InvalidOperationException("No response received from LM Studio");
            }

            var content = response.Choices[0].Message?.Content?.ToString() ?? string.Empty;
            var (generatedPrompt, _) = ParsePromptResponse(content);
            return generatedPrompt;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate video prompt from tags via LM Studio");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> EnhanceVideoPromptAsync(
        string prompt,
        string? negativePrompt = null,
        IEnumerable<string>? loraTriggerWords = null,
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("LM Studio integration is not enabled");
        }

        try
        {
            var directive = useNsfwMode
                ? Settings.VideoGenerationDirectiveNsfw
                : Settings.VideoGenerationDirective;

            var userMessage = $"""
                Please enhance this video prompt for AI video generation:
                {prompt}
                """;

            // Add LoRA trigger words requirement
            var triggerWordsList = loraTriggerWords?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (triggerWordsList is { Count: > 0 })
            {
                var triggerWordsText = string.Join(", ", triggerWordsList);
                userMessage += $"""

                    REQUIRED LORA TRIGGER WORDS (MUST include these in your response):
                    {triggerWordsText}

                    IMPORTANT: Your enhanced prompt MUST include ALL of the above trigger words. These are required to activate LoRA models. Integrate them naturally into the prompt.
                    """;
            }

            if (!string.IsNullOrWhiteSpace(negativePrompt))
            {
                userMessage += $"""

                    NEGATIVE PROMPT (DO NOT include these elements):
                    {negativePrompt}
                    """;
            }

            userMessage += """

                Add details about motion, camera movement, and temporal changes while maintaining the original intent.
                CRITICAL: Your response must NOT contain ANY words or concepts from the negative prompt. Only describe what SHOULD appear.
                Output only the enhanced video prompt, nothing else.
                """;

            var request = new LmStudioChatCompletionRequest
            {
                Model = Settings.TextModel,
                Messages =
                [
                    new LmStudioChatMessage { Role = "system", Content = directive },
                    new LmStudioChatMessage { Role = "user", Content = userMessage },
                ],
                Temperature = Settings.Temperature,
                MaxTokens = Settings.MaxTokens,
                Stream = false,
            };

            var response = await SendChatCompletionAsync(request, cancellationToken);

            if (response?.Choices is not { Count: > 0 })
            {
                throw new InvalidOperationException("No response received from LM Studio");
            }

            var content = response.Choices[0].Message?.Content?.ToString() ?? string.Empty;
            var (generatedPrompt, _) = ParsePromptResponse(content);
            return generatedPrompt;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enhance video prompt via LM Studio");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GenerateVideoPromptFromImageAsync(
        byte[] imageData,
        string? existingPrompt = null,
        string? negativePrompt = null,
        IEnumerable<string>? loraTriggerWords = null,
        bool useNsfwMode = false,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("LM Studio integration is not enabled");
        }

        try
        {
            var directive = useNsfwMode
                ? Settings.VideoGenerationDirectiveNsfw
                : Settings.VideoGenerationDirective;

            var base64Image = Convert.ToBase64String(imageData);
            var dataUri = $"data:image/png;base64,{base64Image}";

            var textPrompt = string.IsNullOrWhiteSpace(existingPrompt)
                ? "Analyze this image and generate a detailed video prompt describing how it could be animated. Include camera movements, subject motion, and atmospheric changes."
                : $"Analyze this image and enhance/expand this video prompt: {existingPrompt}\n\nAdd motion, camera movement, and animation details while keeping the original intent.";

            // Add LoRA trigger words requirement
            var triggerWordsList = loraTriggerWords?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (triggerWordsList is { Count: > 0 })
            {
                var triggerWordsText = string.Join(", ", triggerWordsList);
                textPrompt +=
                    $"\n\nREQUIRED LORA TRIGGER WORDS (MUST include these in your response): {triggerWordsText}\n\nIMPORTANT: Your enhanced prompt MUST include ALL of the above trigger words. These are required to activate LoRA models. Integrate them naturally into the prompt.";
            }

            if (!string.IsNullOrWhiteSpace(negativePrompt))
            {
                textPrompt +=
                    $"\n\nNEGATIVE PROMPT (DO NOT include these elements): {negativePrompt}\n\nCRITICAL: Your response must NOT contain ANY words or concepts from the negative prompt. Only describe what SHOULD appear.";
            }

            var contentParts = new List<object>
            {
                new LmStudioImageContent
                {
                    ImageUrl = new LmStudioImageUrl { Url = dataUri, Detail = "high" },
                },
                new LmStudioTextContent { Text = textPrompt },
            };

            var request = new LmStudioChatCompletionRequest
            {
                Model = Settings.VisionModel ?? Settings.TextModel,
                Messages =
                [
                    new LmStudioChatMessage { Role = "system", Content = directive },
                    new LmStudioChatMessage { Role = "user", Content = contentParts },
                ],
                Temperature = Settings.Temperature,
                MaxTokens = Settings.MaxTokens,
                Stream = false,
            };

            var response = await SendChatCompletionAsync(request, cancellationToken);

            if (response?.Choices is not { Count: > 0 })
            {
                throw new InvalidOperationException("No response received from LM Studio");
            }

            var content = response.Choices[0].Message?.Content?.ToString() ?? string.Empty;
            var (generatedPrompt, _) = ParsePromptResponse(content);
            return generatedPrompt;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate video prompt from image via LM Studio");
            throw;
        }
    }

    private HttpClient CreateHttpClient()
    {
        var client = httpClientFactory.CreateClient("LmStudio");
        client.BaseAddress = new Uri(Settings.EndpointUrl);
        client.Timeout = TimeSpan.FromSeconds(Settings.TimeoutSeconds);
        return client;
    }

    private async Task<LmStudioChatCompletionResponse?> SendChatCompletionAsync(
        LmStudioChatCompletionRequest request,
        CancellationToken cancellationToken
    )
    {
        using var client = CreateHttpClient();
        var response = await client.PostAsJsonAsync(
            "/v1/chat/completions",
            request,
            JsonOptions,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<LmStudioChatCompletionResponse>(
            JsonOptions,
            cancellationToken
        );
    }

    private static string BuildEnhancementUserMessage(
        string prompt,
        string? negativePrompt,
        IEnumerable<string>? loraTriggerWords = null
    )
    {
        var message = $"Enhance the following prompt:\n\n{prompt}";

        // Add LoRA trigger words requirement
        var triggerWordsList = loraTriggerWords?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (triggerWordsList is { Count: > 0 })
        {
            var triggerWordsText = string.Join(", ", triggerWordsList);
            message += $"""

REQUIRED LORA TRIGGER WORDS (MUST include these in your response):
{triggerWordsText}

IMPORTANT: Your enhanced prompt MUST include ALL of the above trigger words. These are required to activate LoRA models. Integrate them naturally into the prompt.
""";
        }

        if (!string.IsNullOrWhiteSpace(negativePrompt))
        {
            message += $"""

NEGATIVE PROMPT (DO NOT include these elements in your response):
{negativePrompt}

CRITICAL: Your enhanced positive prompt must NOT contain ANY words, concepts, or elements from the negative prompt above. The negative prompt lists things to EXCLUDE from the image, so your positive prompt should describe only what SHOULD appear.
""";
        }

        return message;
    }

    private static (string prompt, string? negativePrompt) ParsePromptResponse(string response)
    {
        // Try to parse structured response with positive/negative prompts
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string? positivePrompt = null;
        string? negativePrompt = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (
                trimmedLine.StartsWith("Positive:", StringComparison.OrdinalIgnoreCase)
                || trimmedLine.StartsWith("Positive Prompt:", StringComparison.OrdinalIgnoreCase)
            )
            {
                positivePrompt = trimmedLine.Substring(trimmedLine.IndexOf(':') + 1).Trim();
            }
            else if (
                trimmedLine.StartsWith("Negative:", StringComparison.OrdinalIgnoreCase)
                || trimmedLine.StartsWith("Negative Prompt:", StringComparison.OrdinalIgnoreCase)
            )
            {
                negativePrompt = trimmedLine.Substring(trimmedLine.IndexOf(':') + 1).Trim();
            }
        }

        // If no structured format found, return the whole response as the prompt
        if (string.IsNullOrWhiteSpace(positivePrompt))
        {
            positivePrompt = response.Trim();
        }

        // Sanitize colons that are not inside brackets to prevent prompt syntax errors
        positivePrompt = SanitizePromptColons(positivePrompt);
        if (negativePrompt != null)
        {
            negativePrompt = SanitizePromptColons(negativePrompt);
        }

        return (positivePrompt, negativePrompt);
    }

    /// <summary>
    /// Sanitizes a prompt string by escaping colons that are not inside valid bracket contexts.
    /// This prevents syntax errors when colons are used in text like "character: name".
    /// </summary>
    private static string SanitizePromptColons(string promptText)
    {
        if (string.IsNullOrEmpty(promptText))
            return promptText;

        var result = new System.Text.StringBuilder(promptText.Length + 10);
        var parenDepth = 0;
        var bracketDepth = 0;
        var angleBracketDepth = 0;
        var curlyBracketDepth = 0;

        for (var i = 0; i < promptText.Length; i++)
        {
            var c = promptText[i];
            var prevChar = i > 0 ? promptText[i - 1] : '\0';

            // Track bracket depths (ignoring escaped brackets)
            if (prevChar != '\\')
            {
                switch (c)
                {
                    case '(':
                        parenDepth++;
                        break;
                    case ')':
                        parenDepth = Math.Max(0, parenDepth - 1);
                        break;
                    case '[':
                        bracketDepth++;
                        break;
                    case ']':
                        bracketDepth = Math.Max(0, bracketDepth - 1);
                        break;
                    case '<':
                        angleBracketDepth++;
                        break;
                    case '>':
                        angleBracketDepth = Math.Max(0, angleBracketDepth - 1);
                        break;
                    case '{':
                        curlyBracketDepth++;
                        break;
                    case '}':
                        curlyBracketDepth = Math.Max(0, curlyBracketDepth - 1);
                        break;
                }
            }

            // Handle colons
            if (c == ':' && prevChar != '\\')
            {
                // Check if we're inside any bracket context
                var insideBrackets =
                    parenDepth > 0 || bracketDepth > 0 || angleBracketDepth > 0 || curlyBracketDepth > 0;

                if (!insideBrackets)
                {
                    // Escape the colon
                    result.Append('\\');
                }
            }

            result.Append(c);
        }

        return result.ToString();
    }

    private static string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/png",
        };
    }

    /// <inheritdoc />
    public async Task<bool> UnloadModelsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            logger.LogDebug("LM Studio not enabled, skipping model unload");
            return true;
        }

        try
        {
            logger.LogInformation("Unloading LM Studio models to free VRAM...");

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "lms",
                Arguments = "unload --all",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = new System.Diagnostics.Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode == 0)
            {
                logger.LogInformation("LM Studio models unloaded successfully: {Output}", output.Trim());
                return true;
            }
            else
            {
                logger.LogWarning(
                    "Failed to unload LM Studio models. Exit code: {ExitCode}, Error: {Error}",
                    process.ExitCode,
                    error
                );
                return false;
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            // "lms" command not found - LM Studio CLI not installed or not in PATH
            logger.LogDebug("LM Studio CLI (lms) not found in PATH, skipping model unload");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error unloading LM Studio models");
            return false;
        }
    }
}
