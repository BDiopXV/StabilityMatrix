using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Injectio.Attributes;
using Refit;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.ImgBB;

namespace StabilityMatrix.Core.Services;

public interface ICivitAIUploadCoreService
{
    Task<string> UploadToImgBbAsync(string imagePath);
    Task<CivitAIIntentGetResponse> PostToCivitAIAsync(CivitAIIntentGetRequest payload);
}

[RegisterSingleton<ICivitAIUploadCoreService, CivitAIUploadCoreService>]
public class CivitAIUploadCoreService(IImgBBApi imgbbApi, ICivitIntentApi civitIntentApi)
    : ICivitAIUploadCoreService
{
    public async Task<string> UploadToImgBbAsync(string imagePath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image file not found", imagePath);

        // 1️⃣ Encode file
        var bytes = await File.ReadAllBytesAsync(imagePath).ConfigureAwait(false);
        var base64 = Convert.ToBase64String(bytes);

        // 3️⃣ Build request object (matches PowerShell body)
        var request = new ImgBBUploadImageRequest
        {
            image = base64,
            name = Path.GetFileNameWithoutExtension(imagePath),
            expiration = 300,
        };

        // 4️⃣ Call Refit endpoint
        var resp = await imgbbApi.UploadImage(request);

        // 5️⃣ Extract display URL
        var url = resp?.Data?.DisplayUrl ?? resp?.Data?.Url;
        if (string.IsNullOrWhiteSpace(url))
            throw new Exception("ImgBB upload failed: no URL returned");

        return url!;
    }

    public async Task<CivitAIIntentGetResponse> PostToCivitAIAsync(CivitAIIntentGetRequest payload)
    {
        CivitAIIntentGetResponse lastResponse = await civitIntentApi.GetIntentAsync(payload);

        return lastResponse;
    }
}
