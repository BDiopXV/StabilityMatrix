using StabilityMatrix.Core.Models.Api.ImgBB;

namespace StabilityMatrix.Core.Models.Api;

public class ImgBBAccountStatusUpdateEventArgs : EventArgs
{
    public static ImgBBAccountStatusUpdateEventArgs Disconnected { get; } = new();

    public bool IsConnected { get; init; }
    public string? Username { get; init; } // Optional: if we decide to fetch/display username
    public string? ErrorMessage { get; init; }

    // Constructor to allow initialization, matching the usage in AccountSettingsViewModel
    public ImgBBAccountStatusUpdateEventArgs() { }

    public ImgBBAccountStatusUpdateEventArgs(bool isConnected, string? username, string? errorMessage = null)
    {
        IsConnected = isConnected;
        Username = username;
        ErrorMessage = errorMessage;
    }
}
