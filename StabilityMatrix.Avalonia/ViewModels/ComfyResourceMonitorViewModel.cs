using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

namespace StabilityMatrix.Avalonia.ViewModels;

/// <summary>
/// ViewModel for displaying ComfyUI system resource usage with circular indicators
/// </summary>
[RegisterSingleton<ComfyResourceMonitorViewModel>]
public partial class ComfyResourceMonitorViewModel : ObservableObject, IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IInferenceClientManager clientManager;
    private IDisposable? systemStatsSubscription;

    // Color thresholds
    private const double LowThreshold = 25.0;
    private const double MediumThreshold = 75.0;
    private const double HighThreshold = 80.0;

    // Colors for resource usage indicators
    private static readonly IBrush LowUsageBrush = new SolidColorBrush(Color.Parse("#3498db")); // Blue
    private static readonly IBrush MediumUsageBrush = new SolidColorBrush(Color.Parse("#f39c12")); // Orange
    private static readonly IBrush HighUsageBrush = new SolidColorBrush(Color.Parse("#e74c3c")); // Red

    [ObservableProperty]
    private bool isVisible;

    [ObservableProperty]
    private double cpuUsage;

    [ObservableProperty]
    private IBrush cpuBrush = LowUsageBrush;

    [ObservableProperty]
    private double ramUsage;

    [ObservableProperty]
    private IBrush ramBrush = LowUsageBrush;

    [ObservableProperty]
    private double gpuUsage;

    [ObservableProperty]
    private IBrush gpuBrush = LowUsageBrush;

    [ObservableProperty]
    private double vramUsage;

    [ObservableProperty]
    private IBrush vramBrush = LowUsageBrush;

    [ObservableProperty]
    private double gpuTemperature;

    [ObservableProperty]
    private IBrush temperatureBrush = LowUsageBrush;

    [ObservableProperty]
    private string cpuText = "0%";

    [ObservableProperty]
    private string ramText = "0%";

    [ObservableProperty]
    private string gpuText = "0%";

    [ObservableProperty]
    private string vramText = "0%";

    [ObservableProperty]
    private string temperatureText = "0°C";

    public ComfyResourceMonitorViewModel(IInferenceClientManager clientManager)
    {
        this.clientManager = clientManager;

        // Subscribe to client changes
        clientManager.PropertyChanged += OnClientManagerPropertyChanged;
        TrySubscribeToClient();
    }

    private void OnClientManagerPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e
    )
    {
        if (
            e.PropertyName == nameof(IInferenceClientManager.Client)
            || e.PropertyName == nameof(IInferenceClientManager.IsConnected)
        )
        {
            TrySubscribeToClient();
        }
    }

    private void TrySubscribeToClient()
    {
        // Unsubscribe from previous client
        systemStatsSubscription?.Dispose();
        systemStatsSubscription = null;

        if (clientManager.Client is { } client && clientManager.IsConnected)
        {
            Logger.Debug("Subscribing to ComfyUI system stats");
            client.SystemStatsReceived += OnSystemStatsReceived;
            systemStatsSubscription = new DisposableAction(() =>
                client.SystemStatsReceived -= OnSystemStatsReceived
            );
            IsVisible = true;
        }
        else
        {
            IsVisible = false;
            ResetValues();
        }
    }

    private void OnSystemStatsReceived(object? sender, ComfySystemStats stats)
    {
        Logger.Trace(
            "Received system stats: CPU={CpuUsage}%, RAM={RamUsage}%",
            stats.CpuUtilization,
            stats.RamUsedPercent
        );

        // Update CPU
        CpuUsage = stats.CpuUtilization;
        CpuText = $"{stats.CpuUtilization:F0}%";
        CpuBrush = GetBrushForUsage(stats.CpuUtilization);

        // Update RAM
        RamUsage = stats.RamUsedPercent;
        RamText = $"{stats.RamUsedPercent:F0}%";
        RamBrush = GetBrushForUsage(stats.RamUsedPercent);

        // Update GPU stats if available
        if (stats.Gpus is { Count: > 0 })
        {
            var gpu = stats.Gpus[0];

            GpuUsage = gpu.GpuUtilization;
            GpuText = $"{gpu.GpuUtilization:F0}%";
            GpuBrush = GetBrushForUsage(gpu.GpuUtilization);

            VramUsage = gpu.VramUsedPercent;
            VramText = $"{gpu.VramUsedPercent:F0}%";
            VramBrush = GetBrushForUsage(gpu.VramUsedPercent);

            GpuTemperature = gpu.GpuTemperature;
            TemperatureText = $"{gpu.GpuTemperature:F0}°C";
            TemperatureBrush = GetTemperatureBrush(gpu.GpuTemperature);
        }
    }

    private static IBrush GetBrushForUsage(double usage)
    {
        return usage switch
        {
            < LowThreshold => LowUsageBrush,
            < HighThreshold => MediumUsageBrush,
            _ => HighUsageBrush,
        };
    }

    private static IBrush GetTemperatureBrush(double temperature)
    {
        return temperature switch
        {
            < 50 => LowUsageBrush,
            < 75 => MediumUsageBrush,
            _ => HighUsageBrush,
        };
    }

    private void ResetValues()
    {
        CpuUsage = 0;
        CpuText = "0%";
        CpuBrush = LowUsageBrush;

        RamUsage = 0;
        RamText = "0%";
        RamBrush = LowUsageBrush;

        GpuUsage = 0;
        GpuText = "0%";
        GpuBrush = LowUsageBrush;

        VramUsage = 0;
        VramText = "0%";
        VramBrush = LowUsageBrush;

        GpuTemperature = 0;
        TemperatureText = "0°C";
        TemperatureBrush = LowUsageBrush;
    }

    public void Dispose()
    {
        clientManager.PropertyChanged -= OnClientManagerPropertyChanged;
        systemStatsSubscription?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Helper class for disposal action
    /// </summary>
    private class DisposableAction : IDisposable
    {
        private readonly Action action;

        public DisposableAction(Action action)
        {
            this.action = action;
        }

        public void Dispose() => action();
    }
}
