using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Service for generating random tags from the tag completion CSV
/// </summary>
[RegisterSingleton<IRandomTagService, RandomTagService>]
public class RandomTagService : IRandomTagService
{
    private readonly ILogger<RandomTagService> logger;
    private readonly ISettingsManager settingsManager;

    private readonly AsyncLock loadLock = new();
    private List<TagCsvEntry> allTags = [];
    private List<TagCsvEntry> sfwTags = [];
    private List<string> promptLines = [];
    private bool isLoaded;

    // Tag types that are generally considered SFW
    // Type meanings vary by dataset, but generally:
    // 0 = General, 1 = Artist, 3 = Copyright, 4 = Character, 5 = Meta
    // Types 1 (artist) and some others might need filtering
    private static readonly HashSet<int> SafeTagTypes = [0, 3, 4, 5];

    private static readonly Random Random = new();

    public RandomTagService(ILogger<RandomTagService> logger, ISettingsManager settingsManager)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
    }

    /// <inheritdoc />
    public bool IsReady => isLoaded && allTags.Count > 0;

    /// <inheritdoc />
    public async Task EnsureLoadedAsync()
    {
        if (isLoaded)
            return;

        using var _ = await loadLock.LockAsync();

        if (isLoaded)
            return;

        await LoadTagsAsync();
    }

    private async Task LoadTagsAsync()
    {
        try
        {
            var tagsDir = settingsManager.TagsDirectory;
            var csvPath = settingsManager.Settings.TagCompletionCsv;

            if (string.IsNullOrEmpty(csvPath))
            {
                csvPath = "danbooru.csv";
            }

            var fullPath = tagsDir.JoinFile(csvPath);

            if (!fullPath.Exists)
            {
                logger.LogWarning("Tag CSV file not found: {Path}", fullPath);
                return;
            }

            logger.LogInformation("Loading tags from {Path}", fullPath);

            await using var stream = fullPath.Info.OpenRead();
            var parser = new TagCsvParser(stream);

            allTags = new List<TagCsvEntry>();

            await foreach (var entry in parser.ParseAsync())
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                    continue;

                // Skip tags with very low counts (likely noise)
                if (entry.Count is < 100)
                    continue;

                allTags.Add(entry);
            }

            // Create SFW filtered list (general tags, characters, copyrights)
            sfwTags = allTags.Where(t => t.Type.HasValue && SafeTagTypes.Contains(t.Type.Value)).ToList();

            // Also check for prompts.jsonl in the tags directory and load prompts if present
            try
            {
                var promptsFile = tagsDir.JoinFile("prompts.jsonl");
                if (promptsFile.Exists)
                {
                    logger.LogInformation("Loading prompts from {Path}", promptsFile);
                    promptLines = new List<string>();
                    await using var prStream = promptsFile.Info.OpenRead();
                    using var reader = new StreamReader(prStream);
                    string? line;
                    while ((line = await reader.ReadLineAsync()) is not null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        try
                        {
                            // Try parse as JSON and extract common prompt fields
                            using var doc = System.Text.Json.JsonDocument.Parse(line);
                            var root = doc.RootElement;
                            string? prompt = null;
                            if (root.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                prompt = root.GetString();
                            }
                            else if (root.TryGetProperty("prompt", out var p))
                            {
                                prompt = p.GetString();
                            }
                            else if (root.TryGetProperty("text", out var t))
                            {
                                prompt = t.GetString();
                            }

                            if (string.IsNullOrWhiteSpace(prompt))
                            {
                                // Fallback to raw line
                                prompt = line.Trim();
                            }

                            if (!string.IsNullOrWhiteSpace(prompt))
                                promptLines.Add(prompt!);
                        }
                        catch (System.Text.Json.JsonException)
                        {
                            // Not JSON; use the raw line
                            promptLines.Add(line.Trim());
                        }
                    }

                    logger.LogInformation("Loaded {Count} prompts from prompts.jsonl", promptLines.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load prompts.jsonl");
            }

            isLoaded = true;

            logger.LogInformation("Loaded {Total} tags ({Sfw} SFW)", allTags.Count, sfwTags.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load tags");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetRandomTagsAsync(int count = 10, bool includeNsfw = false)
    {
        await EnsureLoadedAsync();

        var sourceList = includeNsfw ? allTags : sfwTags;

        if (sourceList.Count == 0)
        {
            return [];
        }

        // Weight selection by tag count (more popular tags are more likely)
        var selectedTags = new HashSet<string>();
        var attempts = 0;
        var maxAttempts = count * 10;

        while (selectedTags.Count < count && attempts < maxAttempts)
        {
            attempts++;

            // Use weighted random selection based on count
            var tag = GetWeightedRandomTag(sourceList);

            if (tag?.Name != null)
            {
                // Format tag name (replace underscores with spaces)
                var formattedName = tag.Name.Replace('_', ' ');
                selectedTags.Add(formattedName);
            }
        }

        return selectedTags.ToList();
    }

    /// <inheritdoc />
    public async Task<string> GetRandomPromptAsync(int count = 10, bool includeNsfw = false)
    {
        await EnsureLoadedAsync();

        // If this is NSFW and prompts.jsonl was loaded, prefer returning a random prompt
        if (includeNsfw && promptLines is { Count: > 0 })
        {
            var idx = Random.Next(0, promptLines.Count);
            return promptLines[idx];
        }

        var tags = await GetRandomTagsAsync(count, includeNsfw);
        return string.Join(", ", tags);
    }

    private static TagCsvEntry? GetWeightedRandomTag(List<TagCsvEntry> tags)
    {
        if (tags.Count == 0)
            return null;

        // Calculate total weight (using log scale to not over-bias popular tags)
        var totalWeight = tags.Sum(t => Math.Log10(t.Count ?? 1) + 1);
        var randomValue = Random.NextDouble() * totalWeight;

        double cumulative = 0;
        foreach (var tag in tags)
        {
            cumulative += Math.Log10(tag.Count ?? 1) + 1;
            if (randomValue <= cumulative)
            {
                return tag;
            }
        }

        // Fallback to last tag
        return tags[^1];
    }
}
