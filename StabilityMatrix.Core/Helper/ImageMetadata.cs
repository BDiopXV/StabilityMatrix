using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using ExifLibrary;
using KGySoft.CoreLibraries;
using LibVLCSharp.Shared;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.WebP;
using Microsoft.Extensions.Logging;
using NLog;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using Directory = MetadataExtractor.Directory;
using DrawingSize = System.Drawing.Size;
using VlcCore = global::LibVLCSharp.Shared.Core;

namespace StabilityMatrix.Core.Helper;

public class ImageMetadata
{
    private IReadOnlyList<Directory>? Directories { get; set; }

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly object LibVlcInitLock = new();
    private static bool libVlcInitialized;
    private static readonly TimeSpan VideoSnapshotTimeout = TimeSpan.FromSeconds(3);
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Idat = "IDAT"u8.ToArray();
    private static readonly byte[] Text = "tEXt"u8.ToArray();

    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".webm",
        ".mov",
        ".avi",
    };

    public static IReadOnlyCollection<string> VideoFileExtensions => VideoExtensions;

    public static bool IsVideoFile(FilePath? filePath) =>
        filePath is not null && IsVideoExtension(filePath.Extension);

    public static bool IsVideoExtension(string? extension) =>
        extension is not null && VideoExtensions.Contains(extension);

    public static ImageMetadata ParseFile(FilePath path)
    {
        return new ImageMetadata { Directories = ImageMetadataReader.ReadMetadata(path) };
    }

    public static ImageMetadata ParseFile(Stream stream)
    {
        return new ImageMetadata { Directories = ImageMetadataReader.ReadMetadata(stream) };
    }

    public System.Drawing.Size? GetImageSize()
    {
        if (Directories?.OfType<PngDirectory>().FirstOrDefault() is { } header)
        {
            header.TryGetInt32(PngDirectory.TagImageWidth, out var width);
            header.TryGetInt32(PngDirectory.TagImageHeight, out var height);

            return new System.Drawing.Size(width, height);
        }

        return null;
    }

    public static DrawingSize GetImageSize(byte[] inputImage)
    {
        if (inputImage.Length < 0x18)
        {
            Logger.Warn("GetImageSize(byte[]): buffer too short ({Length} bytes)", inputImage.Length);
            return new DrawingSize(0, 0);
        }

        return GetImageSizeInternal(inputImage.AsSpan());
    }

    public static DrawingSize GetImageSize(BinaryReader reader)
    {
        var oldPosition = reader.BaseStream.Position;

        try
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < 0x18)
            {
                Logger.Warn(
                    "GetImageSize(BinaryReader): stream too short ({Length} bytes)"
                        + " at position {Position}",
                    reader.BaseStream.Length,
                    reader.BaseStream.Position
                );
                return new DrawingSize(0, 0);
            }

            reader.BaseStream.Position = 0x10;
            var buffer = reader.ReadBytes(8);

            if (buffer.Length < 8)
            {
                Logger.Warn("GetImageSize(BinaryReader): failed to read 8 bytes at offset 0x10");
                return new DrawingSize(0, 0);
            }

            return GetImageSizeInternal(buffer);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Logger.Warn(ex, "GetImageSize(BinaryReader): invalid offset while parsing size");
            return new DrawingSize(0, 0);
        }
        finally
        {
            reader.BaseStream.Position = oldPosition;
        }
    }

    private static DrawingSize GetImageSizeInternal(ReadOnlySpan<byte> buffer)
    {
        var imageWidth = BinaryPrimitives.ReadInt32BigEndian(buffer[0..4]);
        var imageHeight = BinaryPrimitives.ReadInt32BigEndian(buffer[4..8]);

        return new DrawingSize(imageWidth, imageHeight);
    }

    public static (
        string? POSprompt,
        string? NEGprompt,
        string? Parameters,
        string? ParametersJson,
        string? SMProject,
        string? ComfyNodes,
        string? CivitParameters,
        GenerationParameters? VideoGenerationParameters
    ) GetAllFileMetadata(FilePath filePath)
    {
        if (IsVideoFile(filePath))
        {
            var videoParameters = ParseVideoGenerationParameters(filePath);
            return (
                videoParameters?.PositivePrompt,
                videoParameters?.NegativePrompt,
                null,
                null,
                null,
                null,
                null,
                videoParameters
            );
        }

        if (filePath.Extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            var paramsJson = ReadTextChunkFromWebp(filePath, ExifDirectoryBase.TagImageDescription);
            var smProj = ReadTextChunkFromWebp(filePath, ExifDirectoryBase.TagSoftware);

            return (null, null, null, paramsJson, smProj, null, null, null);
        }

        if (
            filePath.Extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || filePath.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
        )
        {
            var file = ImageFile.FromFile(filePath.Info.FullName);
            var userComment = file.Properties.Get(ExifTag.UserComment);
            var bytes = userComment.Interoperability.Data.Skip(8).ToArray();
            var userCommentString = Encoding.BigEndianUnicode.GetString(bytes);

            return (null, null, null, null, null, null, userCommentString, null);
        }

        using var stream = filePath.Info.OpenRead();
        using var reader = new BinaryReader(stream);

        var promptJSON = ReadTextChunk(reader, "prompt");

        var prompt = System.Text.Json.JsonDocument.Parse(promptJSON).RootElement;

        string POS_promptText = prompt
            .GetProperty("PositiveCLIP_Base")
            .GetProperty("inputs")
            .GetProperty("text")
            .GetString();

        string NEG_promptText = prompt
            .GetProperty("NegativeCLIP_Base")
            .GetProperty("inputs")
            .GetProperty("text")
            .GetString();

        // Logger.Info("Loaded Image metadata Positive'{Meta}'", POS_promptText);
        // Logger.Info("Loaded Image metadata Positive'{Meta}'", NEG_promptText);

        var parameters = ReadTextChunk(reader, "parameters");
        var parametersJson = ReadTextChunk(reader, "parameters-json");
        var smProject = ReadTextChunk(reader, "smproj");
        var comfyNodes = ReadTextChunk(reader, "prompt");
        var civitParameters = ReadTextChunk(reader, "user_comment");

        return (
            string.IsNullOrEmpty(POS_promptText) ? null : POS_promptText,
            string.IsNullOrEmpty(NEG_promptText) ? null : NEG_promptText,
            string.IsNullOrEmpty(parameters) ? null : parameters,
            string.IsNullOrEmpty(parametersJson) ? null : parametersJson,
            string.IsNullOrEmpty(smProject) ? null : smProject,
            string.IsNullOrEmpty(comfyNodes) ? null : comfyNodes,
            string.IsNullOrEmpty(civitParameters) ? null : civitParameters,
            null
        );
    }

    public static GenerationParameters? ParseVideoGenerationParameters(FilePath filePath)
    {
        if (!filePath.Extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var payload = ReadMp4Comment(filePath);
        if (payload is null || payload.Length == 0)
        {
            return null;
        }

        foreach (var decoder in new[] { Encoding.UTF8, Encoding.Latin1 })
        {
            var candidate = decoder.GetString(payload).TrimEnd('\0');
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var generationParameters = ParseGenerationParametersFromVideoComment(candidate);
            if (generationParameters is not null)
            {
                return generationParameters;
            }
        }

        return null;
    }

    public static byte[]? ReadEmbeddedVideoPreview(FilePath filePath)
    {
        if (!filePath.Extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!EnsureLibVlcInitialized())
        {
            return null;
        }

        var snapshotPath = Path.Combine(
            Path.GetTempPath(),
            $"StabilityMatrixVideoPreview-{Guid.NewGuid():N}.png"
        );

        try
        {
            using var libVlc = new LibVLC(
                "--quiet",
                "--no-osd",
                "--no-video-title-show",
                "--no-audio",
                "--avcodec-hw=none",
                "--vout=dummy"
            );
            using var media = new Media(
                libVlc,
                filePath.FullPath,
                FromType.FromPath,
                ":no-video-title-show",
                ":no-audio",
                ":avcodec-hw=none",
                ":vout=dummy"
            );
            using var mediaPlayer = new MediaPlayer(media) { EnableHardwareDecoding = false };

            mediaPlayer.Play();
            Thread.Sleep(250);

            if (!mediaPlayer.TakeSnapshot(0, snapshotPath, 0, 0))
            {
                mediaPlayer.Stop();
                return null;
            }

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < VideoSnapshotTimeout)
            {
                if (File.Exists(snapshotPath) && new FileInfo(snapshotPath).Length > 0)
                {
                    break;
                }

                Thread.Sleep(50);
            }

            mediaPlayer.Stop();

            if (!File.Exists(snapshotPath) || new FileInfo(snapshotPath).Length == 0)
            {
                return null;
            }

            return File.ReadAllBytes(snapshotPath);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to capture video preview for '{Path}'", filePath.FullPath);
            return null;
        }
        finally
        {
            TryDeleteTempFile(snapshotPath);
        }
    }

    public static void TryWriteVideoPreviewSidecar(FilePath videoFile)
    {
        try
        {
            var previewBytes = ReadEmbeddedVideoPreview(videoFile);
            if (previewBytes is null || previewBytes.Length == 0)
            {
                return;
            }

            var previewPath = Path.ChangeExtension(videoFile.FullPath, ".png");
            if (previewPath is null)
            {
                return;
            }

            File.WriteAllBytes(previewPath, previewBytes);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to extract embedded preview for '{Path}'", videoFile.FullPath);
        }
    }

    private static bool EnsureLibVlcInitialized()
    {
        if (libVlcInitialized)
        {
            return true;
        }

        lock (LibVlcInitLock)
        {
            if (libVlcInitialized)
            {
                return true;
            }

            try
            {
                VlcCore.Initialize();
                libVlcInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to initialize LibVLC for video previews");
                return false;
            }
        }
    }

    private static void TryDeleteTempFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception)
        {
            // best effort cleanup, swallow failures
        }
    }

    private static byte[]? ReadMp4Comment(FilePath filePath)
    {
        try
        {
            using var stream = filePath.Info.OpenRead();
            using var reader = new BinaryReader(stream);
            return FindMp4Comment(reader, stream.Length);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to read MP4 metadata from '{Path}'", filePath.FullPath);
            return null;
        }
    }

    private static byte[]? FindMp4Comment(BinaryReader reader, long containerEnd)
    {
        while (reader.BaseStream.Position < containerEnd)
        {
            if (!TryReadMp4AtomHeader(reader, containerEnd, out var header))
            {
                break;
            }

            var atomEnd = Math.Min(header.Start + header.Size, containerEnd);
            byte[]? comment = null;

            switch (header.Type)
            {
                case "meta":
                    if (atomEnd - reader.BaseStream.Position >= 4)
                    {
                        reader.BaseStream.Position += 4;
                        comment = FindMp4Comment(reader, atomEnd);
                    }
                    break;
                case "moov":
                case "udta":
                case "ilst":
                case "trak":
                case "mdia":
                case "minf":
                case "stbl":
                    comment = FindMp4Comment(reader, atomEnd);
                    break;
                case "©cmt":
                    comment = ParseMp4CommentData(reader, atomEnd);
                    break;
            }

            reader.BaseStream.Position = atomEnd;
            if (comment is not null)
            {
                return comment;
            }
        }

        return null;
    }

    private static byte[]? ParseMp4CommentData(BinaryReader reader, long atomEnd)
    {
        while (reader.BaseStream.Position < atomEnd)
        {
            if (!TryReadMp4AtomHeader(reader, atomEnd, out var header))
            {
                break;
            }

            var dataEnd = Math.Min(header.Start + header.Size, atomEnd);

            if (header.Type == "data")
            {
                var contentStart = reader.BaseStream.Position;
                if (dataEnd - contentStart < 8)
                {
                    reader.BaseStream.Position = dataEnd;
                    continue;
                }

                reader.BaseStream.Position += 8;
                var payloadLength = (int)Math.Max(0, dataEnd - reader.BaseStream.Position);
                var payload = reader.ReadBytes(payloadLength);
                reader.BaseStream.Position = dataEnd;
                return payload;
            }

            reader.BaseStream.Position = dataEnd;
        }

        return null;
    }

    private static uint ReadUInt32BigEndian(BinaryReader reader)
    {
        var buffer = reader.ReadBytes(4);
        if (buffer.Length < 4)
        {
            throw new EndOfStreamException();
        }

        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    private static ulong ReadUInt64BigEndian(BinaryReader reader)
    {
        var buffer = reader.ReadBytes(8);
        if (buffer.Length < 8)
        {
            throw new EndOfStreamException();
        }

        return BinaryPrimitives.ReadUInt64BigEndian(buffer);
    }

    private static bool TryReadMp4AtomHeader(BinaryReader reader, long containerEnd, out Mp4AtomHeader header)
    {
        header = default;
        var start = reader.BaseStream.Position;

        if (start + 8 > containerEnd)
        {
            return false;
        }

        var size32 = ReadUInt32BigEndian(reader);
        var typeBytes = reader.ReadBytes(4);
        if (typeBytes.Length < 4)
        {
            return false;
        }

        var headerSize = 8L;
        long size = size32;

        if (size32 == 1)
        {
            if (reader.BaseStream.Position + 8 > containerEnd)
            {
                return false;
            }

            size = (long)ReadUInt64BigEndian(reader);
            headerSize = 16;
        }
        else if (size32 == 0)
        {
            size = containerEnd - start;
        }

        if (size < headerSize)
        {
            return false;
        }

        var type = Encoding.Latin1.GetString(typeBytes);
        header = new Mp4AtomHeader(start, size, headerSize, type);
        return true;
    }

    private static GenerationParameters? ParseGenerationParametersFromVideoComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(comment);
            var root = NormalizeCommentRoot(document.RootElement);

            var generationParameters = new GenerationParameters();
            var hasData = false;

            if (
                TryGetPromptString(
                    root,
                    new[] { "WanVideoTextEncode", "inputs", "positive_prompt" },
                    out var positive
                )
            )
            {
                generationParameters.PositivePrompt = positive;
                hasData |= !string.IsNullOrWhiteSpace(positive);
            }
            else if (TryFindFirstStringValue(root, "positive_prompt", out positive))
            {
                generationParameters.PositivePrompt = positive;
                hasData |= !string.IsNullOrWhiteSpace(positive);
            }

            if (
                TryGetPromptString(
                    root,
                    new[] { "WanVideoTextEncode", "inputs", "negative_prompt" },
                    out var negative
                )
            )
            {
                generationParameters.NegativePrompt = negative;
                hasData |= !string.IsNullOrWhiteSpace(negative);
            }
            else if (TryFindFirstStringValue(root, "negative_prompt", out negative))
            {
                generationParameters.NegativePrompt = negative;
                hasData |= !string.IsNullOrWhiteSpace(negative);
            }

            if (
                TryGetInt(root, new[] { "WanVideoSampler_High", "inputs", "steps" }, out var steps)
                || TryGetInt(root, new[] { "WanVideoSampler_Low", "inputs", "steps" }, out steps)
                || TryFindFirstIntValue(root, "steps", out steps)
            )
            {
                generationParameters.Steps = steps;
                hasData = true;
            }

            if (TryGetString(root, new[] { "WanVideoSampler_High", "inputs", "scheduler" }, out var sampler))
            {
                generationParameters.Sampler = sampler;
                hasData = true;
            }

            if (
                TryGetDouble(root, new[] { "WanVideoSampler_High", "inputs", "cfg" }, out var cfg)
                || TryFindFirstDoubleValue(root, "cfg", out cfg)
            )
            {
                generationParameters.CfgScale = cfg;
                hasData = true;
            }

            if (
                TryGetUInt64(root, new[] { "WanVideoSampler_High", "inputs", "seed" }, out var seed)
                || TryGetUInt64(root, new[] { "WanVideoSampler_Low", "inputs", "seed" }, out seed)
                || TryFindFirstUInt64Value(root, "seed", out seed)
            )
            {
                generationParameters.Seed = seed;
                hasData = true;
            }

            if (TryGetInt(root, new[] { "WanVideoImageToVideoEncode", "inputs", "width" }, out var width))
            {
                generationParameters.Width = width;
                hasData = true;
            }

            if (TryGetInt(root, new[] { "WanVideoImageToVideoEncode", "inputs", "height" }, out var height))
            {
                generationParameters.Height = height;
                hasData = true;
            }

            if (
                TryGetInt(
                    root,
                    new[] { "WanVideoImageToVideoEncode", "inputs", "num_frames" },
                    out var frameCount
                )
            )
            {
                generationParameters.FrameCount = frameCount;
                hasData = true;
            }

            if (TryGetDouble(root, new[] { "VHS_VideoCombine", "inputs", "frame_rate" }, out var frameRate))
            {
                generationParameters.Fps = (int)Math.Round(frameRate);
                generationParameters.OutputFps = frameRate;
                hasData = true;
            }

            if (
                TryGetString(root, new[] { "WanVideoModelLoader_High", "inputs", "model" }, out var modelHigh)
            )
            {
                generationParameters = generationParameters with { ModelNameHigh = modelHigh };
                hasData = true;
            }

            if (TryGetString(root, new[] { "WanVideoModelLoader_Low", "inputs", "model" }, out var modelLow))
            {
                generationParameters = generationParameters with { ModelNameLow = modelLow };
                hasData = true;
            }

            if (TryGetString(root, new[] { "WanVideoVAELoader", "inputs", "model_name" }, out var vaeName))
            {
                generationParameters.VaeName = vaeName;
                hasData = true;
            }

            generationParameters.ModelName ??=
                generationParameters.ModelNameHigh ?? generationParameters.ModelNameLow;

            return hasData ? generationParameters : null;
        }
        catch (JsonException ex)
        {
            Logger.Debug(ex, "Failed to parse MP4 metadata comment");
            return null;
        }
    }

    private static JsonElement NormalizeCommentRoot(JsonElement element)
    {
        var current = element;
        while (true)
        {
            if (TryParseNestedJson(current, out var nestedJson))
            {
                current = nestedJson;
                continue;
            }

            if (
                current.ValueKind == JsonValueKind.Object
                && TryFindProperty(current, "prompt", out var promptElement)
            )
            {
                current = promptElement;
                continue;
            }

            break;
        }

        return current;
    }

    private static bool TryGetPromptString(JsonElement element, string[] path, out string? value)
    {
        value = null;
        if (TryGetNestedElement(element, path, out var result) && result.ValueKind == JsonValueKind.String)
        {
            value = result.GetString();
            return true;
        }

        return false;
    }

    private static bool TryGetNestedElement(
        JsonElement element,
        IReadOnlyList<string> path,
        out JsonElement result
    )
    {
        result = element;
        foreach (var segment in path)
        {
            if (!TryFindProperty(result, segment, out result))
            {
                result = default;
                return false;
            }
        }

        return true;
    }

    private static bool TryFindProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (JsonPropertyMatches(property.Name, propertyName))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetString(JsonElement element, IReadOnlyList<string> path, out string? value)
    {
        value = null;
        if (TryGetNestedElement(element, path, out var result))
        {
            if (result.ValueKind == JsonValueKind.String)
            {
                value = result.GetString();
                return true;
            }

            if (result.ValueKind == JsonValueKind.Number)
            {
                value = result.GetRawText();
                return true;
            }
        }

        return false;
    }

    private static bool TryGetInt(JsonElement element, IReadOnlyList<string> path, out int value)
    {
        value = default;
        if (TryGetNestedElement(element, path, out var result))
        {
            if (result.TryGetInt32(out value))
            {
                return true;
            }

            if (result.TryGetInt64(out var longValue))
            {
                value = (int)Math.Clamp(longValue, int.MinValue, int.MaxValue);
                return true;
            }

            if (
                result.ValueKind == JsonValueKind.String
                && int.TryParse(
                    result.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetDouble(JsonElement element, IReadOnlyList<string> path, out double value)
    {
        value = default;
        if (TryGetNestedElement(element, path, out var result))
        {
            if (result.TryGetDouble(out value))
            {
                return true;
            }

            if (
                result.ValueKind == JsonValueKind.String
                && double.TryParse(
                    result.GetString(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsed
                )
            )
            {
                value = parsed;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetUInt64(JsonElement element, IReadOnlyList<string> path, out ulong value)
    {
        value = default;
        if (TryGetNestedElement(element, path, out var result))
        {
            if (result.TryGetUInt64(out value))
            {
                return true;
            }

            if (result.TryGetInt64(out var longValue) && longValue >= 0)
            {
                value = (ulong)longValue;
                return true;
            }

            if (
                result.ValueKind == JsonValueKind.String
                && ulong.TryParse(
                    result.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed
                )
            )
            {
                value = parsed;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindFirstStringValue(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (
                    JsonPropertyMatches(property.Name, propertyName)
                    && property.Value.ValueKind == JsonValueKind.String
                )
                {
                    value = property.Value.GetString();
                    return true;
                }

                if (TryFindFirstStringValue(property.Value, propertyName, out value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindFirstStringValue(item, propertyName, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryFindFirstIntValue(JsonElement element, string propertyName, out int value)
    {
        value = default;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (JsonPropertyMatches(property.Name, propertyName))
                {
                    if (property.Value.TryGetInt32(out value))
                    {
                        return true;
                    }

                    if (
                        property.Value.ValueKind == JsonValueKind.String
                        && int.TryParse(
                            property.Value.GetString(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out value
                        )
                    )
                    {
                        return true;
                    }
                }

                if (TryFindFirstIntValue(property.Value, propertyName, out value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindFirstIntValue(item, propertyName, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryFindFirstDoubleValue(JsonElement element, string propertyName, out double value)
    {
        value = default;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (JsonPropertyMatches(property.Name, propertyName))
                {
                    if (property.Value.TryGetDouble(out value))
                    {
                        return true;
                    }

                    if (
                        property.Value.ValueKind == JsonValueKind.String
                        && double.TryParse(
                            property.Value.GetString(),
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out value
                        )
                    )
                    {
                        return true;
                    }
                }

                if (TryFindFirstDoubleValue(property.Value, propertyName, out value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindFirstDoubleValue(item, propertyName, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryFindFirstUInt64Value(JsonElement element, string propertyName, out ulong value)
    {
        value = default;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (JsonPropertyMatches(property.Name, propertyName))
                {
                    if (property.Value.TryGetUInt64(out value))
                    {
                        return true;
                    }

                    if (
                        property.Value.ValueKind == JsonValueKind.String
                        && ulong.TryParse(
                            property.Value.GetString(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out value
                        )
                    )
                    {
                        return true;
                    }
                }

                if (TryFindFirstUInt64Value(property.Value, propertyName, out value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindFirstUInt64Value(item, propertyName, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private readonly record struct Mp4AtomHeader(long Start, long Size, long HeaderSize, string Type);

    private static bool JsonPropertyMatches(string actual, string expected)
    {
        if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(
            NormalizePropertyName(actual),
            NormalizePropertyName(expected),
            StringComparison.Ordinal
        );
    }

    private static string NormalizePropertyName(string value) =>
        value
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

    private static bool TryParseNestedJson(JsonElement element, out JsonElement nested)
    {
        nested = default;
        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = element.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            nested = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public IEnumerable<Tag>? GetTextualData()
    {
        // Get the PNG-tEXt directory
        return Directories
            ?.Where(d => d.Name == "PNG-tEXt")
            .SelectMany(d => d.Tags)
            .Where(t => t.Name == "Textual Data");
    }

    public GenerationParameters? GetGenerationParameters()
    {
        var textualData = GetTextualData()?.ToArray();
        if (textualData is null)
        {
            return null;
        }

        // Use "parameters-json" tag if exists
        if (
            textualData.FirstOrDefault(tag =>
                tag.Description is { } desc && desc.StartsWith("parameters-json: ")
            ) is
            { Description: { } description }
        )
        {
            description = description.StripStart("parameters-json: ");

            return JsonSerializer.Deserialize<GenerationParameters>(description);
        }

        // Otherwise parse "parameters" tag
        if (
            textualData.FirstOrDefault(tag =>
                tag.Description is { } desc && desc.StartsWith("parameters: ")
            ) is
            { Description: { } parameters }
        )
        {
            parameters = parameters.StripStart("parameters: ");

            if (GenerationParameters.TryParse(parameters, out var generationParameters))
            {
                return generationParameters;
            }
        }

        return null;
    }

    public static string ReadTextChunk(BinaryReader byteStream, string key)
    {
        byteStream.BaseStream.Position = 0;

        // Read first 8 bytes and make sure they match the png header
        if (!byteStream.ReadBytes(8).SequenceEqual(PngHeader))
        {
            return string.Empty;
        }

        while (byteStream.BaseStream.Position < byteStream.BaseStream.Length - 4)
        {
            var chunkSize = BitConverter.ToInt32(byteStream.ReadBytes(4).AsEnumerable().Reverse().ToArray());
            var chunkType = Encoding.UTF8.GetString(byteStream.ReadBytes(4));

            if (chunkType == Encoding.UTF8.GetString(Idat))
            {
                return string.Empty;
            }

            if (chunkType == Encoding.UTF8.GetString(Text))
            {
                var textBytes = byteStream.ReadBytes(chunkSize);
                var text = Encoding.UTF8.GetString(textBytes);
                if (text.StartsWith($"{key}\0"))
                {
                    return text[(key.Length + 1)..];
                }
            }
            else
            {
                // skip chunk data
                byteStream.BaseStream.Position += chunkSize;
            }

            // skip crc
            byteStream.BaseStream.Position += 4;
        }

        return string.Empty;
    }

    public static MemoryStream? BuildImageWithoutMetadata(FilePath imagePath)
    {
        using var byteStream = new BinaryReader(File.OpenRead(imagePath));
        byteStream.BaseStream.Position = 0;

        if (!byteStream.ReadBytes(8).SequenceEqual(PngHeader))
        {
            return null;
        }

        var memoryStream = new MemoryStream();
        memoryStream.Write(PngHeader);

        // add the IHDR chunk
        var ihdrStuff = byteStream.ReadBytes(25);
        memoryStream.Write(ihdrStuff);

        // find IDATs
        while (byteStream.BaseStream.Position < byteStream.BaseStream.Length - 4)
        {
            var chunkSizeBytes = byteStream.ReadBytes(4);
            var chunkSize = BitConverter.ToInt32(chunkSizeBytes.AsEnumerable().Reverse().ToArray());
            var chunkTypeBytes = byteStream.ReadBytes(4);
            var chunkType = Encoding.UTF8.GetString(chunkTypeBytes);

            if (chunkType != Encoding.UTF8.GetString(Idat))
            {
                // skip chunk data
                byteStream.BaseStream.Position += chunkSize;
                // skip crc
                byteStream.BaseStream.Position += 4;
                continue;
            }

            memoryStream.Write(chunkSizeBytes);
            memoryStream.Write(chunkTypeBytes);
            var idatBytes = byteStream.ReadBytes(chunkSize);
            memoryStream.Write(idatBytes);
            var crcBytes = byteStream.ReadBytes(4);
            memoryStream.Write(crcBytes);
        }

        // Add IEND chunk
        memoryStream.Write([0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82]);
        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>
    /// Reads an EXIF tag from a webp file and returns the value as string
    /// </summary>
    /// <param name="filePath">The webp file to read EXIF data from</param>
    /// <param name="exifTag">Use <see cref="ExifDirectoryBase"/> constants for the tag you'd like to search for</param>
    /// <returns></returns>
    public static string ReadTextChunkFromWebp(FilePath filePath, int exifTag)
    {
        var exifDirs = WebPMetadataReader.ReadMetadata(filePath).OfType<ExifIfd0Directory>().FirstOrDefault();
        return exifDirs is null ? string.Empty : exifDirs.GetString(exifTag) ?? string.Empty;
    }

    public static IEnumerable<byte> AddMetadataToWebp(
        byte[] inputImage,
        Dictionary<ExifTag, string> exifTagData
    )
    {
        using var byteStream = new BinaryReader(new MemoryStream(inputImage));
        byteStream.BaseStream.Position = 0;

        // Read first 8 bytes and make sure they match the RIFF header
        if (!byteStream.ReadBytes(4).SequenceEqual(Riff))
        {
            return Array.Empty<byte>();
        }

        // skip 4 bytes then read next 4 for webp header
        byteStream.BaseStream.Position += 4;
        if (!byteStream.ReadBytes(4).SequenceEqual(Webp))
        {
            return Array.Empty<byte>();
        }

        while (byteStream.BaseStream.Position < byteStream.BaseStream.Length - 4)
        {
            var chunkType = Encoding.UTF8.GetString(byteStream.ReadBytes(4));
            var chunkSize = BitConverter.ToInt32(byteStream.ReadBytes(4).ToArray());

            if (chunkType != "EXIF")
            {
                // skip chunk data
                byteStream.BaseStream.Position += chunkSize;
                continue;
            }

            var exifStart = byteStream.BaseStream.Position - 8;
            var exifBytes = byteStream.ReadBytes(chunkSize);
            Debug.WriteLine($"Found exif chunk of size {chunkSize}");

            using var stream = new MemoryStream(exifBytes[6..]);
            var img = new MyTiffFile(stream, Encoding.UTF8);

            foreach (var (key, value) in exifTagData)
            {
                img.Properties.Set(key, value);
            }

            using var newStream = new MemoryStream();
            img.Save(newStream);
            newStream.Seek(0, SeekOrigin.Begin);
            var newExifBytes = exifBytes[..6].Concat(newStream.ToArray());
            var newExifSize = newExifBytes.Count();
            var newChunkSize = BitConverter.GetBytes(newExifSize);
            var newChunk = "EXIF"u8.ToArray().Concat(newChunkSize).Concat(newExifBytes).ToArray();

            var inputEndIndex = (int)exifStart;
            var newImage = inputImage[..inputEndIndex].Concat(newChunk).ToArray();

            // webp or tiff or something requires even number of bytes
            if (newImage.Length % 2 != 0)
            {
                newImage = newImage.Concat(new byte[] { 0x00 }).ToArray();
            }

            // no clue why the minus 8 is needed but it is
            var newImageSize = BitConverter.GetBytes(newImage.Length - 8);
            newImage[4] = newImageSize[0];
            newImage[5] = newImageSize[1];
            newImage[6] = newImageSize[2];
            newImage[7] = newImageSize[3];
            return newImage;
        }

        return Array.Empty<byte>();
    }

    private static byte[] GetExifChunks(FilePath imagePath)
    {
        using var byteStream = new BinaryReader(File.OpenRead(imagePath));
        byteStream.BaseStream.Position = 0;

        // Read first 8 bytes and make sure they match the RIFF header
        if (!byteStream.ReadBytes(4).SequenceEqual(Riff))
        {
            return Array.Empty<byte>();
        }

        // skip 4 bytes then read next 4 for webp header
        byteStream.BaseStream.Position += 4;
        if (!byteStream.ReadBytes(4).SequenceEqual(Webp))
        {
            return Array.Empty<byte>();
        }

        while (byteStream.BaseStream.Position < byteStream.BaseStream.Length - 4)
        {
            var chunkType = Encoding.UTF8.GetString(byteStream.ReadBytes(4));
            var chunkSize = BitConverter.ToInt32(byteStream.ReadBytes(4).ToArray());

            if (chunkType != "EXIF")
            {
                // skip chunk data
                byteStream.BaseStream.Position += chunkSize;
                continue;
            }

            var exifStart = byteStream.BaseStream.Position;
            var exifBytes = byteStream.ReadBytes(chunkSize);
            var exif = Encoding.UTF8.GetString(exifBytes);
            Debug.WriteLine($"Found exif chunk of size {chunkSize}");
            return exifBytes;
        }

        return Array.Empty<byte>();
    }
}
