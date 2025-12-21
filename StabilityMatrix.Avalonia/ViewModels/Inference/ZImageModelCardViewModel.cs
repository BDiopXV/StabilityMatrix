using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ZImageModelCard))]
[ManagedService]
[RegisterTransient<ZImageModelCardViewModel>]
public partial class ZImageModelCardViewModel(IInferenceClientManager clientManager)
    : LoadableViewModelBase,
        IParametersLoadableState,
        IComfyStep
{
    [ObservableProperty]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    private HybridModelFile? selectedVae;

    [ObservableProperty]
    private HybridModelFile? selectedClip;

    [ObservableProperty]
    private string selectedDType = "default";

    public List<string> WeightDTypes { get; set; } = ["default", "fp8_e4m3fn", "fp8_e5m2"];

    public IInferenceClientManager ClientManager { get; } = clientManager;

    public async Task<bool> ValidateModel()
    {
        if (SelectedModel != null)
            return true;

        var dialog = DialogHelper.CreateMarkdownDialog(
            "Please select a model to continue.",
            "No Model Selected"
        );
        await dialog.ShowAsync();
        return false;
    }

    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        // UNETLoader for ZImage
        var checkpointLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.UNETLoader
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.UNETLoader)),
                UnetName = SelectedModel?.RelativePath ?? throw new ValidationException("No Model Selected"),
                WeightDtype = SelectedDType,
            }
        );
        e.Builder.Connections.Base.Model = checkpointLoader.Output;

        // VAELoader
        var vaeLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.VAELoader
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAELoader)),
                VaeName = SelectedVae?.RelativePath ?? throw new ValidationException("No VAE Selected"),
            }
        );
        e.Builder.Connections.Base.VAE = vaeLoader.Output;

        // Single CLIPLoader for ZImage
        var clipLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPLoader
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPLoader)),
                ClipName = SelectedClip?.RelativePath ?? throw new ValidationException("No CLIP Selected"),
                Type = "sd3",
            }
        );
        e.Builder.Connections.Base.Clip = clipLoader.Output;
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        return SerializeModel(
            new ZImageModelCardModel
            {
                SelectedModelName = SelectedModel?.RelativePath,
                SelectedVaeName = SelectedVae?.RelativePath,
                SelectedClipName = SelectedClip?.RelativePath,
                SelectedDType = SelectedDType,
            }
        );
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<ZImageModelCardModel>(state);

        SelectedModel = model.SelectedModelName is null
            ? null
            : ClientManager.UnetModels.FirstOrDefault(x => x.RelativePath == model.SelectedModelName);

        SelectedVae = model.SelectedVaeName is null
            ? HybridModelFile.Default
            : ClientManager.VaeModels.FirstOrDefault(x => x.RelativePath == model.SelectedVaeName);

        SelectedClip = model.SelectedClipName is null
            ? HybridModelFile.None
            : ClientManager.ClipModels.FirstOrDefault(x => x.RelativePath == model.SelectedClipName);

        SelectedDType = model.SelectedDType ?? "default";
    }

    internal class ZImageModelCardModel
    {
        public string? SelectedModelName { get; set; }
        public string? SelectedVaeName { get; set; }
        public string? SelectedClipName { get; set; }
        public string? SelectedDType { get; set; }
    }

    /// <inheritdoc />
    public void LoadStateFromParameters(GenerationParameters parameters)
    {
        if (parameters.ModelName is not { } paramsModelName)
            return;

        var currentModels = ClientManager.UnetModels;

        HybridModelFile? model;

        // First try hash match
        if (parameters.ModelHash is not null)
        {
            model = currentModels.FirstOrDefault(m =>
                m.Local?.ConnectedModelInfo?.Hashes.SHA256 is { } sha256
                && sha256.StartsWith(parameters.ModelHash, StringComparison.InvariantCultureIgnoreCase)
            );
        }
        else
        {
            // Name matches
            model = currentModels.FirstOrDefault(m => m.RelativePath.EndsWith(paramsModelName));
            model ??= currentModels.FirstOrDefault(m => m.ShortDisplayName.StartsWith(paramsModelName));
        }

        if (model is not null)
        {
            SelectedModel = model;
        }
    }

    /// <inheritdoc />
    public GenerationParameters SaveStateToParameters(GenerationParameters parameters)
    {
        return parameters with
        {
            ModelName = SelectedModel?.FileName,
            ModelHash = SelectedModel?.Local?.ConnectedModelInfo?.Hashes.SHA256,
        };
    }
}
