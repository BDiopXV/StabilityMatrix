using System.Collections.ObjectModel;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using FluentIcons.Common;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Settings;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.LmStudio;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels.Settings;

[View(typeof(LmStudioSettingsPage))]
[ManagedService]
[RegisterSingleton<LmStudioSettingsViewModel>]
public partial class LmStudioSettingsViewModel : PageViewModelBase
{
    private readonly ILogger<LmStudioSettingsViewModel> logger;
    private readonly ISettingsManager settingsManager;
    private readonly ILmStudioService lmStudioService;
    private readonly INotificationService notificationService;

    /// <inheritdoc />
    public override string Title => "LM Studio";

    /// <inheritdoc />
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Bot, IconVariant = IconVariant.Filled };

    [ObservableProperty]
    private bool isEnabled;

    [ObservableProperty]
    private string endpointUrl = "http://localhost:1234";

    [ObservableProperty]
    private string? textModel;

    [ObservableProperty]
    private string? visionModel;

    [ObservableProperty]
    private string textEnhancementDirective = LmStudioSettings.DefaultTextEnhancementDirective;

    [ObservableProperty]
    private string textEnhancementDirectiveNsfw = LmStudioSettings.DefaultTextEnhancementDirectiveNsfw;

    [ObservableProperty]
    private string imageAnalysisDirective = LmStudioSettings.DefaultImageAnalysisDirective;

    [ObservableProperty]
    private string imageAnalysisDirectiveNsfw = LmStudioSettings.DefaultImageAnalysisDirectiveNsfw;

    [ObservableProperty]
    private string videoGenerationDirective = LmStudioSettings.DefaultVideoGenerationDirective;

    [ObservableProperty]
    private string videoGenerationDirectiveNsfw = LmStudioSettings.DefaultVideoGenerationDirectiveNsfw;

    [ObservableProperty]
    private double temperature = 0.7;

    [ObservableProperty]
    private int maxTokens = 500;

    [ObservableProperty]
    private int timeoutSeconds = 60;

    [ObservableProperty]
    private bool autoEnhancePrompts;

    [ObservableProperty]
    private bool isTestingConnection;

    [ObservableProperty]
    private bool isConnectionSuccessful;

    [ObservableProperty]
    private string? connectionStatusMessage;

    [ObservableProperty]
    private ObservableCollection<string> availableModels = [];

    private bool isLoading;

    public LmStudioSettingsViewModel(
        ILogger<LmStudioSettingsViewModel> logger,
        ISettingsManager settingsManager,
        ILmStudioService lmStudioService,
        INotificationService notificationService
    )
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.lmStudioService = lmStudioService;
        this.notificationService = notificationService;
    }

    /// <inheritdoc />
    public override void OnLoaded()
    {
        base.OnLoaded();
        LoadSettings();
    }

    private void LoadSettings()
    {
        isLoading = true;
        try
        {
            var settings = settingsManager.Settings.LmStudioSettings ?? new LmStudioSettings();

            IsEnabled = settings.IsEnabled;
            EndpointUrl = settings.EndpointUrl;
            TextModel = settings.TextModel;
            VisionModel = settings.VisionModel;
            TextEnhancementDirective = settings.TextEnhancementDirective;
            TextEnhancementDirectiveNsfw = settings.TextEnhancementDirectiveNsfw;
            ImageAnalysisDirective = settings.ImageAnalysisDirective;
            ImageAnalysisDirectiveNsfw = settings.ImageAnalysisDirectiveNsfw;
            VideoGenerationDirective = settings.VideoGenerationDirective;
            VideoGenerationDirectiveNsfw = settings.VideoGenerationDirectiveNsfw;
            Temperature = settings.Temperature;
            MaxTokens = settings.MaxTokens;
            TimeoutSeconds = settings.TimeoutSeconds;
            AutoEnhancePrompts = settings.AutoEnhancePrompts;
        }
        finally
        {
            isLoading = false;
        }
    }

    private void SaveSettings()
    {
        if (isLoading)
            return;

        settingsManager.Transaction(s =>
        {
            s.LmStudioSettings ??= new LmStudioSettings();
            s.LmStudioSettings.IsEnabled = IsEnabled;
            s.LmStudioSettings.EndpointUrl = EndpointUrl;
            s.LmStudioSettings.TextModel = TextModel;
            s.LmStudioSettings.VisionModel = VisionModel;
            s.LmStudioSettings.TextEnhancementDirective = TextEnhancementDirective;
            s.LmStudioSettings.TextEnhancementDirectiveNsfw = TextEnhancementDirectiveNsfw;
            s.LmStudioSettings.ImageAnalysisDirective = ImageAnalysisDirective;
            s.LmStudioSettings.ImageAnalysisDirectiveNsfw = ImageAnalysisDirectiveNsfw;
            s.LmStudioSettings.VideoGenerationDirective = VideoGenerationDirective;
            s.LmStudioSettings.VideoGenerationDirectiveNsfw = VideoGenerationDirectiveNsfw;
            s.LmStudioSettings.Temperature = Temperature;
            s.LmStudioSettings.MaxTokens = MaxTokens;
            s.LmStudioSettings.TimeoutSeconds = TimeoutSeconds;
            s.LmStudioSettings.AutoEnhancePrompts = AutoEnhancePrompts;
        });
    }

    partial void OnIsEnabledChanged(bool value) => SaveSettings();

    partial void OnEndpointUrlChanged(string value) => SaveSettings();

    partial void OnTextModelChanged(string? value) => SaveSettings();

    partial void OnVisionModelChanged(string? value) => SaveSettings();

    partial void OnTextEnhancementDirectiveChanged(string value) => SaveSettings();

    partial void OnTextEnhancementDirectiveNsfwChanged(string value) => SaveSettings();

    partial void OnImageAnalysisDirectiveChanged(string value) => SaveSettings();

    partial void OnImageAnalysisDirectiveNsfwChanged(string value) => SaveSettings();

    partial void OnVideoGenerationDirectiveChanged(string value) => SaveSettings();

    partial void OnVideoGenerationDirectiveNsfwChanged(string value) => SaveSettings();

    partial void OnTemperatureChanged(double value) => SaveSettings();

    partial void OnMaxTokensChanged(int value) => SaveSettings();

    partial void OnTimeoutSecondsChanged(int value) => SaveSettings();

    partial void OnAutoEnhancePromptsChanged(bool value) => SaveSettings();

    [RelayCommand]
    private async Task TestConnection()
    {
        IsTestingConnection = true;
        ConnectionStatusMessage = "Testing connection...";
        IsConnectionSuccessful = false;

        try
        {
            // Temporarily update settings to test with current values
            SaveSettings();

            var success = await lmStudioService.TestConnectionAsync();

            if (success)
            {
                IsConnectionSuccessful = true;
                ConnectionStatusMessage = "Connection successful!";

                // Refresh available models
                await RefreshModels();

                notificationService.Show(
                    "LM Studio Connected",
                    "Successfully connected to LM Studio",
                    NotificationType.Success
                );
            }
            else
            {
                ConnectionStatusMessage = "Connection failed. Is LM Studio running?";
                notificationService.Show(
                    "Connection Failed",
                    "Could not connect to LM Studio. Make sure it's running and the server is started.",
                    NotificationType.Error
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to test LM Studio connection");
            ConnectionStatusMessage = $"Error: {ex.Message}";
            notificationService.Show("Connection Error", ex.Message, NotificationType.Error);
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    [RelayCommand]
    private async Task RefreshModels()
    {
        try
        {
            SaveSettings();
            var models = await lmStudioService.GetModelsAsync();

            AvailableModels.Clear();
            foreach (var model in models)
            {
                AvailableModels.Add(model.Id);
            }

            if (models.Count > 0)
            {
                notificationService.Show(
                    "Models Loaded",
                    $"Found {models.Count} model(s)",
                    NotificationType.Information
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh LM Studio models");
            notificationService.Show("Error Loading Models", ex.Message, NotificationType.Error);
        }
    }

    [RelayCommand]
    private void ResetTextDirective()
    {
        TextEnhancementDirective = LmStudioSettings.DefaultTextEnhancementDirective;
    }

    [RelayCommand]
    private void ResetTextDirectiveNsfw()
    {
        TextEnhancementDirectiveNsfw = LmStudioSettings.DefaultTextEnhancementDirectiveNsfw;
    }

    [RelayCommand]
    private void ResetImageDirective()
    {
        ImageAnalysisDirective = LmStudioSettings.DefaultImageAnalysisDirective;
    }

    [RelayCommand]
    private void ResetImageDirectiveNsfw()
    {
        ImageAnalysisDirectiveNsfw = LmStudioSettings.DefaultImageAnalysisDirectiveNsfw;
    }

    [RelayCommand]
    private void ResetVideoDirective()
    {
        VideoGenerationDirective = LmStudioSettings.DefaultVideoGenerationDirective;
    }

    [RelayCommand]
    private void ResetVideoDirectiveNsfw()
    {
        VideoGenerationDirectiveNsfw = LmStudioSettings.DefaultVideoGenerationDirectiveNsfw;
    }
}
