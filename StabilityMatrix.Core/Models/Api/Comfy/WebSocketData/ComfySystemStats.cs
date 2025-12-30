using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

/// <summary>
/// System statistics data from ComfyUI websocket
/// </summary>
public class ComfySystemStats
{
    [JsonPropertyName("cpu_utilization")]
    public double CpuUtilization { get; set; }

    [JsonPropertyName("ram_total")]
    public long RamTotal { get; set; }

    [JsonPropertyName("ram_used")]
    public long RamUsed { get; set; }

    [JsonPropertyName("ram_used_percent")]
    public double RamUsedPercent { get; set; }

    [JsonPropertyName("hdd_total")]
    public long HddTotal { get; set; }

    [JsonPropertyName("hdd_used")]
    public long HddUsed { get; set; }

    [JsonPropertyName("hdd_used_percent")]
    public double HddUsedPercent { get; set; }

    [JsonPropertyName("device_type")]
    public string? DeviceType { get; set; }

    [JsonPropertyName("gpus")]
    public List<ComfyGpuStats>? Gpus { get; set; }
}

/// <summary>
/// GPU statistics from ComfyUI
/// </summary>
public class ComfyGpuStats
{
    [JsonPropertyName("gpu_utilization")]
    public double GpuUtilization { get; set; }

    [JsonPropertyName("gpu_temperature")]
    public double GpuTemperature { get; set; }

    [JsonPropertyName("vram_total")]
    public long VramTotal { get; set; }

    [JsonPropertyName("vram_used")]
    public long VramUsed { get; set; }

    [JsonPropertyName("vram_used_percent")]
    public double VramUsedPercent { get; set; }
}
