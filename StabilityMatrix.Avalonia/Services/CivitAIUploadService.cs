using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Avalonia.Controls.Notifications;
using Injectio.Attributes;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<ICivitAIUploadService, CivitAIUploadService>]
public class CivitAIUploadService(
    INotificationService notificationService,
    ICivitIntentApi civitIntentApi,
    ICivitAIUploadCoreService uploadCoreService
) : ICivitAIUploadService
{
    /// <summary>
    /// Upload an image to ImgBB, then post its URL to CivitAI via Post Intent.
    /// Returns the hosted image URL on success.
    /// </summary>
    public async Task<string> UploadToCivitAIAsync(
        string imagePath,
        string title,
        string description = "",
        string[]? tags = null
    )
    {
        try
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Image not found.", imagePath);

            // 1️⃣ Upload to ImgBB (delegated to core service)
            string imageUrl = await uploadCoreService.UploadToImgBbAsync(imagePath);
            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new Exception("Failed to retrieve ImgBB image URL.");

            // 2️⃣ Post Intent to CivitAI
            await PostToCivitAIAsync(imageUrl, title, description, tags);

            notificationService.Show(
                new Notification(
                    "Upload Successful",
                    $"Image uploaded to CivitAI successfully:\n{imageUrl}",
                    NotificationType.Success
                )
            );

            return imageUrl;
        }
        catch (Exception ex)
        {
            notificationService.Show(
                new Notification("CivitAI Upload Failed", ex.Message, NotificationType.Error)
            );

            throw;
        }
    }

    // helper that posts the intent to CivitAI
    private async Task PostToCivitAIAsync(string imageUrl, string title, string description, string[]? tags)
    {
        var payload = new CivitAIIntentGetRequest
        {
            MediaUrl = imageUrl,
            Title = title,
            Description = description,
            Tags = tags != null ? string.Join(",", tags.Take(5)) : null,
        };

        var civitaiUrl =
            $"https://civitai.com/intent/post?mediaUrl={Uri.EscapeDataString(imageUrl)}"
            + $"&title={Uri.EscapeDataString(title)}"
            + (
                !string.IsNullOrWhiteSpace(description)
                    ? $"&description={Uri.EscapeDataString(description)}"
                    : ""
            )
            + (tags != null && tags.Any() ? $"&tags={Uri.EscapeDataString(string.Join(",", tags))}" : "");

        Process.Start(new ProcessStartInfo { FileName = civitaiUrl, UseShellExecute = true });
    }
}

public interface ICivitAIUploadService
{
    /// <summary>
    /// Uploads the image to ImgBB then posts the CivitAI intent.
    /// Returns the hosted image URL on success.
    /// </summary>
    Task<string> UploadToCivitAIAsync(
        string imagePath,
        string title,
        string description = "",
        string[]? tags = null
    );
}
