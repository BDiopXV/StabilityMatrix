using System.Collections.Generic;
using System.Threading.Tasks;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Service for generating random tags from the tag completion CSV
/// </summary>
public interface IRandomTagService
{
    /// <summary>
    /// Whether the service has tags loaded and ready
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Gets a random selection of tags
    /// </summary>
    /// <param name="count">Number of tags to select</param>
    /// <param name="includeNsfw">Whether to include NSFW tags (types that might be adult content)</param>
    /// <returns>List of random tag names</returns>
    Task<IReadOnlyList<string>> GetRandomTagsAsync(int count = 10, bool includeNsfw = false);

    /// <summary>
    /// Gets random tags formatted as a comma-separated prompt string
    /// </summary>
    /// <param name="count">Number of tags to select</param>
    /// <param name="includeNsfw">Whether to include NSFW tags</param>
    /// <returns>Formatted prompt string</returns>
    Task<string> GetRandomPromptAsync(int count = 10, bool includeNsfw = false);

    /// <summary>
    /// Ensures tags are loaded
    /// </summary>
    Task EnsureLoadedAsync();
}
