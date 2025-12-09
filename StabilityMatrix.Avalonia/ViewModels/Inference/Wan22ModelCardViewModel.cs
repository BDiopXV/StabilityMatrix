using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

// Change inheritance to be derived from ModelCardViewModel so we can correctly override base behaviors.
[View(typeof(Wan22ModelCard))]
[ManagedService]
[RegisterTransient<Wan22ModelCardViewModel>]
public partial class Wan22ModelCardViewModel(
    IInferenceClientManager clientManager,
    IServiceManager<ViewModelBase> vmFactory,
    TabContext tabContext
) : ModelCardViewModel(clientManager, vmFactory, tabContext)
{
    [ObservableProperty]
    private HybridModelFile? selectedModelHigh;

    [ObservableProperty]
    private HybridModelFile? selectedModelLow;

    [ObservableProperty]
    private HybridModelFile? selectedClipModel;

    [ObservableProperty]
    private HybridModelFile? selectedClipVisionModel;

    [ObservableProperty]
    private HybridModelFile? selectedVae;

    [ObservableProperty]
    private string? selectedDType = "fp16_fast";

    [ObservableProperty]
    private bool isClipVisionEnabled;

    [ObservableProperty]
    private double shift = 8.0d;

    // NEW: LoRA lists for High and Low
    [ObservableProperty]
    private List<string> selectedLorasHigh = new();

    [ObservableProperty]
    private List<string> selectedLorasLow = new();

    [ObservableProperty]
    private bool extraNetworksHIsExpanded;

    [ObservableProperty]
    private bool extraNetworksLIsExpanded;

    public StackEditableCardViewModel ExtraNetworksHighStack { get; } =
        new(vmFactory) { Title = "LoRAs (High Model)", AvailableModules = [typeof(Wan22LoraModule)] };

    public StackEditableCardViewModel ExtraNetworksLowStack { get; } =
        new(vmFactory) { Title = "LoRAs (Low Model)", AvailableModules = [typeof(Wan22LoraModule)] };

    public ComfyNodeBuilder.WanVideoModelLoader? highModelLoader;
    public ComfyNodeBuilder.WanVideoModelLoader? lowModelLoader;
    public ComfyNodeBuilder.WanVideoBlockSwap? blockSwap;
    public ComfyNodeBuilder.WanVideoSetBlockSwap? setBlockSwapHigh;
    public ComfyNodeBuilder.WanVideoSetBlockSwap? setBlockSwapLow;
    public ComfyNodeBuilder.WanVideoVAELoader? vaeLoader;
    private readonly ILogger<PromptWan22CardViewModel> logger;

    public new async Task<bool> ValidateModel()
    {
        if (SelectedModelHigh == null || SelectedModelLow == null)
        {
            var dialog = DialogHelper.CreateMarkdownDialog(
                "Please select a HIGH and LOW model to continue.",
                "No Model Selected"
            );
            await dialog.ShowAsync();
            return false;
        }

        if (SelectedVae == null)
        {
            var dialog = DialogHelper.CreateMarkdownDialog(
                "Please select a VAE model to continue.",
                "No VAE Model Selected"
            );
            await dialog.ShowAsync();
            return false;
        }

        if (SelectedClipModel == null)
        {
            var dialog = DialogHelper.CreateMarkdownDialog(
                "Please select a CLIP model to continue.",
                "No CLIP Model Selected"
            );
            await dialog.ShowAsync();
            return false;
        }

        // if (IsClipVisionEnabled && SelectedClipVisionModel == null)
        // {
        //     var dialog = DialogHelper.CreateMarkdownDialog(
        //         "Please select a CLIP Vision model to continue.",
        //         "No CLIP Vision Model Selected"
        //     );
        //     await dialog.ShowAsync();
        //     return false;
        // }

        return true;
    }

    protected override void OnInitialLoaded()
    {
        base.OnInitialLoaded();

        // subscribe to both high/low stacks
        ExtraNetworksHighStack.CardAdded += ExtraNetworksCardAdded;
        ExtraNetworksLowStack.CardAdded += ExtraNetworksCardAdded;
    }

    public override void OnUnloaded()
    {
        base.OnUnloaded();

        // unsubscribe both high/low stacks
        ExtraNetworksHighStack.CardAdded -= ExtraNetworksCardAdded;
        ExtraNetworksLowStack.CardAdded -= ExtraNetworksCardAdded;

        // detach property handlers to avoid leaks for both stacks
        foreach (var module in ExtraNetworksHighStack.Cards.OfType<Wan22LoraModule>())
        {
            if (module.GetCard<ExtraNetworkCardViewModel>() is ExtraNetworkCardViewModel cardVm)
                cardVm.PropertyChanged -= ExtraNetworkCardOnPropertyChanged;
        }

        foreach (var module in ExtraNetworksLowStack.Cards.OfType<Wan22LoraModule>())
        {
            if (module.GetCard<ExtraNetworkCardViewModel>() is ExtraNetworkCardViewModel cardVm)
                cardVm.PropertyChanged -= ExtraNetworkCardOnPropertyChanged;
        }
    }

    private void ExtraNetworksCardAdded(object? sender, LoadableViewModelBase e)
    {
        // Determine target from which stack raised the event
        var stack = sender as StackEditableCardViewModel;
        var targetKey =
            stack == ExtraNetworksHighStack ? "High"
            : stack == ExtraNetworksLowStack ? "Low"
            : null;

        if (targetKey is null)
            return;

        // find concrete module (Wan22LoraModule) that hosts the added card, set title and subscribe
        foreach (var module in stack.Cards.OfType<Wan22LoraModule>())
        {
            if (
                module.GetCard<ExtraNetworkCardViewModel>() is ExtraNetworkCardViewModel cardVm
                && cardVm == e
            )
            {
                module.TargetModelKey = targetKey; // SET HERE
                module.Title = cardVm.SelectedModel?.ShortDisplayName ?? Resources.Label_ExtraNetworks;
                cardVm.PropertyChanged -= ExtraNetworkCardOnPropertyChanged;
                cardVm.PropertyChanged += ExtraNetworkCardOnPropertyChanged;
                return;
            }
        }
    }

    private void ExtraNetworkCardOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ExtraNetworkCardViewModel.SelectedModel))
            return;

        if (sender is not ExtraNetworkCardViewModel cardVm)
            return;

        // locate module that contains this card and update its title for both stacks
        foreach (var stack in new[] { ExtraNetworksHighStack, ExtraNetworksLowStack })
        {
            foreach (var module in stack.Cards)
            {
                if (module is Wan22LoraModule lora && lora.GetCard<ExtraNetworkCardViewModel>() == cardVm)
                {
                    lora.Title = cardVm.SelectedModel?.ShortDisplayName ?? Resources.Label_ExtraNetworks;
                    return;
                }
            }
        }
    }

    // Ensure this is hiding the base non-virtual interface implementation (ModelCardViewModel implements it,
    // but it wasn't virtual). Use 'new' so we intentionally replace the behavior for Wan22 models.
    public new void LoadStateFromParameters(GenerationParameters parameters)
    {
        var currentModels = ClientManager.UnetModels.ToList();
        var currentExtraNetworks = ClientManager.LoraModels.ToList();

        HybridModelFile? modelHigh = null;
        HybridModelFile? modelLow = null;

        // Load High model
        if (parameters.ModelNameHigh is not null)
        {
            if (parameters.ModelHashHigh is not null)
            {
                modelHigh = currentModels.FirstOrDefault(m =>
                    m.Local?.ConnectedModelInfo?.Hashes.SHA256 is { } sha256
                    && sha256.StartsWith(
                        parameters.ModelHashHigh,
                        StringComparison.InvariantCultureIgnoreCase
                    )
                );
            }
            else
            {
                modelHigh = currentModels.FirstOrDefault(m =>
                    m.RelativePath.EndsWith(parameters.ModelNameHigh)
                );
                modelHigh ??= currentModels.FirstOrDefault(m =>
                    m.ShortDisplayName.StartsWith(parameters.ModelNameHigh)
                );
            }
        }

        // Load Low model
        if (parameters.ModelNameLow is not null)
        {
            if (parameters.ModelHashLow is not null)
            {
                modelLow = currentModels.FirstOrDefault(m =>
                    m.Local?.ConnectedModelInfo?.Hashes.SHA256 is { } sha256
                    && sha256.StartsWith(parameters.ModelHashLow, StringComparison.InvariantCultureIgnoreCase)
                );
            }
            else
            {
                modelLow = currentModels.FirstOrDefault(m =>
                    m.RelativePath.EndsWith(parameters.ModelNameLow)
                );
                modelLow ??= currentModels.FirstOrDefault(m =>
                    m.ShortDisplayName.StartsWith(parameters.ModelNameLow)
                );
            }
        }

        if (modelHigh is not null)
            SelectedModelHigh = modelHigh;

        if (modelLow is not null)
            SelectedModelLow = modelLow;

        ExtraNetworksHighStack.Clear();
        ExtraNetworksLowStack.Clear();

        // Load High stack LoRAs
        if (parameters.ExtraNetworkModelVersionIdsHigh is not null)
        {
            IsExtraNetworksEnabled = true;
            foreach (var versionId in parameters.ExtraNetworkModelVersionIdsHigh)
            {
                var module = ExtraNetworksHighStack.AddModule<Wan22LoraModule>();
                module.TargetModelKey = "High"; // SET HERE
                module.GetCard<ExtraNetworkCardViewModel>().SelectedModel =
                    currentExtraNetworks.FirstOrDefault(m =>
                        m.Local?.ConnectedModelInfo?.VersionId == versionId
                    );
                module.IsEnabled = true;
            }
        }

        // Load Low stack LoRAs
        if (parameters.ExtraNetworkModelVersionIdsLow is not null)
        {
            IsExtraNetworksEnabled = true;
            foreach (var versionId in parameters.ExtraNetworkModelVersionIdsLow)
            {
                var module = ExtraNetworksLowStack.AddModule<Wan22LoraModule>();
                module.TargetModelKey = "Low"; // SET HERE
                module.GetCard<ExtraNetworkCardViewModel>().SelectedModel =
                    currentExtraNetworks.FirstOrDefault(m =>
                        m.Local?.ConnectedModelInfo?.VersionId == versionId
                    );
                module.IsEnabled = true;
            }
        }

        // Backwards compatibility: if only single ExtraNetworkModelVersionIds exists, populate High stack
        if (
            parameters.ExtraNetworkModelVersionIds is not null
            && parameters.ExtraNetworkModelVersionIdsHigh is null
            && parameters.ExtraNetworkModelVersionIdsLow is null
        )
        {
            IsExtraNetworksEnabled = true;

            foreach (var versionId in parameters.ExtraNetworkModelVersionIds)
            {
                var module = ExtraNetworksHighStack.AddModule<Wan22LoraModule>();
                module.TargetModelKey = "High"; // SET HERE
                module.GetCard<ExtraNetworkCardViewModel>().SelectedModel =
                    currentExtraNetworks.FirstOrDefault(m =>
                        m.Local?.ConnectedModelInfo?.VersionId == versionId
                    );
                module.IsEnabled = true;
            }
        }
    }

    public new GenerationParameters SaveStateToParameters(GenerationParameters parameters)
    {
        return parameters with
        {
            ModelNameHigh = SelectedModelHigh?.FileName,
            ModelHashHigh = SelectedModelHigh?.Local?.ConnectedModelInfo?.Hashes.SHA256,
            ModelNameLow = SelectedModelLow?.FileName,
            ModelHashLow = SelectedModelLow?.Local?.ConnectedModelInfo?.Hashes.SHA256,
        };
    }

    // ApplyStep should replace / hide base behavior for model load & LoRA hooking.
    // Use 'new' if base doesn't expose a virtual ApplyStep; use override if base's method is virtual.
    public override void ApplyStep(ModuleApplyStepEventArgs e)
    {
        Debug.WriteLine("===== Wan22ModelCardViewModel.ApplyStep START =====");

        try
        {
            // Collect enabled LoRAs from High stack
            var highLoras = ExtraNetworksHighStack
                .Cards.OfType<Wan22LoraModule>()
                .Where(m => m.IsEnabled)
                .Select(m => m.GetCard<ExtraNetworkCardViewModel>())
                .Where(c => c?.SelectedModel is not null)
                .Select(c => (Path: c.SelectedModel!.RelativePath, Weight: c.ModelWeight))
                .ToList();

            // Collect enabled LoRAs from Low stack
            var lowLoras = ExtraNetworksLowStack
                .Cards.OfType<Wan22LoraModule>()
                .Where(m => m.IsEnabled)
                .Select(m => m.GetCard<ExtraNetworkCardViewModel>())
                .Where(c => c?.SelectedModel is not null)
                .Select(c => (Path: c.SelectedModel!.RelativePath, Weight: c.ModelWeight))
                .ToList();

            Debug.WriteLine(
                $"About to call BuildModelWithLoraChain for High model: {SelectedModelHigh?.RelativePath}"
            );
            Debug.WriteLine($"High LoRAs count: {highLoras.Count}");

            e.Builder.Connections.PrimaryShift = Shift;

            var torchCompile = e.Builder.Nodes.AddTypedNode(
                new ComfyNodeBuilder.WanVideoTorchCompileSettings
                {
                    Name = e.Builder.Nodes.GetUniqueName("WanVideoTorchCompileSettings_Global"),
                    Backend = "inductor",
                    FullGraph = false,
                    Mode = "default",
                    Dynamic = false,
                    CacheSizeLimit = 64,
                    CompileTransformerBlocksOnly = true,
                    RecompileLimit = 128,
                }
            );

            // BlockSwap broadcast
            var blockSwap = e.Builder.Nodes.AddTypedNode(
                new ComfyNodeBuilder.WanVideoBlockSwap
                {
                    Name = e.Builder.Nodes.GetUniqueName("WanVideoBlockSwap"),
                    CompileArgs = torchCompile.Output,
                    BlocksToSwap = 40,
                    OffloadImageEmb = false,
                    OffloadTextEmb = false,
                    UseNonBlocking = true,
                }
            );

            // Build High model with chained LoRAs
            var highModelOutput = BuildModelWithLoraChain(
                e,
                "High",
                SelectedModelHigh?.RelativePath ?? throw new ValidationException("High model not selected"),
                highLoras,
                torchCompile.Output,
                blockSwap.Output
            );

            Debug.WriteLine($"High model output created: {highModelOutput?.Data?[0]}");

            // Build Low model with chained LoRAs
            var lowModelOutput = BuildModelWithLoraChain(
                e,
                "Low",
                SelectedModelLow?.RelativePath ?? throw new ValidationException("Low model not selected"),
                lowLoras,
                torchCompile.Output,
                blockSwap.Output
            );

            // Store in connections
            e.Builder.Connections.Models["High"] = new ModelConnections(e.Builder.Connections.Base)
            {
                Name = "High",
                Model = highModelOutput,
            };

            e.Builder.Connections.Models["Low"] = new ModelConnections(e.Builder.Connections.Base)
            {
                Name = "Low",
                Model = lowModelOutput,
            };

            var setBlockSwapHigh = e.Builder.Nodes.AddTypedNode(
                new ComfyNodeBuilder.WanVideoSetBlockSwap
                {
                    Name = e.Builder.Nodes.GetUniqueName("WanVideoSetBlockSwap_High"),
                    Model = highModelOutput,
                    BlockSwapArgs = blockSwap.Output,
                }
            );

            var setBlockSwapLow = e.Builder.Nodes.AddTypedNode(
                new ComfyNodeBuilder.WanVideoSetBlockSwap
                {
                    Name = e.Builder.Nodes.GetUniqueName("WanVideoSetBlockSwap_Low"),
                    Model = lowModelOutput,
                    BlockSwapArgs = blockSwap.Output,
                }
            );

            // Update connections with block-swapped models
            e.Builder.Connections.Models["High"].Model = setBlockSwapHigh.Output;
            e.Builder.Connections.Models["Low"].Model = setBlockSwapLow.Output;

            // VAE loader
            var vaeLoader = e.Builder.Nodes.AddTypedNode(
                new ComfyNodeBuilder.WanVideoVAELoader
                {
                    Name = e.Builder.Nodes.GetUniqueName("WanVideoVAELoader"),
                    CompileArgs = torchCompile.Output,
                    ModelName = SelectedVae?.RelativePath ?? throw new ValidationException("No VAE selected"),
                    Precision = "bf16",
                }
            );
            e.Builder.Connections.Base.VAE = vaeLoader.Output;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EXCEPTION in ApplyStep: {ex}");
            throw;
        }
        finally
        {
            Debug.WriteLine($"Total nodes in dictionary: {e.Builder.Nodes.Count}");

            foreach (var kvp in e.Builder.Nodes)
            {
                Debug.WriteLine(
                    $"  Node: {kvp.Key}, ClassType: {kvp.Value.ClassType}, Inputs count: {kvp.Value.Inputs.Count}"
                );

                // Print actual input values
                foreach (var input in kvp.Value.Inputs)
                {
                    var valueStr = input.Value switch
                    {
                        null => "null",
                        object[] arr => $"[{string.Join(", ", arr.Select(x => x?.ToString() ?? "null"))}]",
                        NodeConnectionBase conn => $"NodeConnection(Data={conn.Data?[0]}, {conn.Data?[1]})",
                        _ => input.Value.ToString(),
                    };
                    Debug.WriteLine($"    {input.Key} = {valueStr}");
                }
            }

            Debug.WriteLine("===== Wan22ModelCardViewModel.ApplyStep END =====");
        }
    }

    private ModelNodeConnection BuildModelWithLoraChain(
        ModuleApplyStepEventArgs e,
        string modelName,
        string modelPath,
        List<(string Path, double Weight)> loras,
        CompileArgsNodeConnection? compileArgs = null,
        BlockSwapNodeConnection? blockSwap = null
    )
    {
        ModelNodeConnection? loraChainOutput = null;

        // Build LoRA chain first (if any LoRAs exist)
        if (loras.Count > 0)
        {
            ModelNodeConnection? currentLoraInput = null;

            foreach (var lora in loras)
            {
                var loraNode = e.Builder.Nodes.AddNamedNode(
                    ComfyNodeBuilder.WanVideoLoraSelect(
                        e.Builder.Nodes.GetUniqueName($"Lora_{modelName}"),
                        currentLoraInput, // null for first LoRA, then chains
                        lora.Path,
                        lora.Weight,
                        MergeLoras: false,
                        LowMemLoad: false
                    )
                );

                // Chain: next LoRA takes previous output as input
                currentLoraInput = loraNode.Output1;
            }

            loraChainOutput = currentLoraInput;
        }

        // Load model with chained LoRA output as input
        var modelLoader = e.Builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.WanVideoModelLoader
            {
                Name = e.Builder.Nodes.GetUniqueName($"WanVideoModelLoader_{modelName}"),
                CompileArgs = compileArgs,
                BlockSwapArgs = blockSwap,
                Model = modelPath,
                Lora = loraChainOutput, // Feed the final chained LoRA output here
                BasePrecision = "fp16_fast",
                Quantization = "disabled",
                LoadDevice = "main_device",
                AttentionMode = "sageattn",
                RmsNormFunction = "default",
            }
        );

        e.Builder.Connections.Models[modelName] = new ModelConnections(e.Builder.Connections.Base)
        {
            Name = modelName,
            Model = modelLoader.Output,
        };

        return modelLoader.Output;
    }

    // Helper to extract LoRA names
    private static List<string> GetSelectedLoras(StackEditableCardViewModel stack) =>
        stack
            .Cards.OfType<Wan22LoraModule>()
            .Select(m => m.GetCard<ExtraNetworkCardViewModel>())
            .Where(card => card?.SelectedModel != null)
            .Select(card => card!.SelectedModel!.RelativePath)
            .ToList();

    public override JsonObject SaveStateToJsonObject()
    {
        return SerializeModel(
            new Wan22ModelModel
            {
                SelectedModelHighName = SelectedModelHigh?.RelativePath,
                SelectedModelLowName = SelectedModelLow?.RelativePath,
                SelectedVaeName = SelectedVae?.RelativePath,
                SelectedClipModelName = SelectedClipModel?.RelativePath,
                // SelectedClipVisionModelName = SelectedClipVisionModel?.RelativePath,
                SelectedDType = SelectedDType,
                IsClipVisionEnabled = IsClipVisionEnabled,
                Shift = Shift,
                ExtraNetworksHigh = ExtraNetworksHighStack.SaveStateToJsonObject(),
                ExtraNetworksLow = ExtraNetworksLowStack.SaveStateToJsonObject(),
            }
        );
    }

    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<Wan22ModelModel>(state);

        // map selected models back to available client lists (hybrid files)
        SelectedModelHigh = model.SelectedModelHighName is null
            ? null
            : ClientManager
                .Models.Concat(ClientManager.UnetModels)
                .FirstOrDefault(x => x.RelativePath == model.SelectedModelHighName);

        SelectedModelLow = model.SelectedModelLowName is null
            ? null
            : ClientManager
                .Models.Concat(ClientManager.UnetModels)
                .FirstOrDefault(x => x.RelativePath == model.SelectedModelLowName);

        SelectedVae = model.SelectedVaeName is null
            ? HybridModelFile.Default
            : ClientManager.VaeModels.FirstOrDefault(x => x.RelativePath == model.SelectedVaeName);

        SelectedClipModel = model.SelectedClipModelName is null
            ? HybridModelFile.None
            : ClientManager.ClipModels.FirstOrDefault(x => x.RelativePath == model.SelectedClipModelName);

        SelectedClipVisionModel = model.SelectedClipVisionModelName is null
            ? HybridModelFile.None
            : ClientManager.ClipModels.FirstOrDefault(x =>
                x.RelativePath == model.SelectedClipVisionModelName
            );

        SelectedDType = model.SelectedDType;
        IsClipVisionEnabled = model.IsClipVisionEnabled;
        Shift = model.Shift;

        if (model.ExtraNetworksHigh is not null)
        {
            ExtraNetworksHighStack.LoadStateFromJsonObject(model.ExtraNetworksHigh);
            MigrateStackModulesToWan22(ExtraNetworksHighStack);

            Dispatcher.UIThread.Post(
                () =>
                {
                    foreach (var module in ExtraNetworksHighStack.Cards.OfType<Wan22LoraModule>())
                    {
                        module.TargetModelKey = "High"; // ENSURE HERE
                        if (module.GetCard<ExtraNetworkCardViewModel>() is ExtraNetworkCardViewModel card)
                        {
                            module.Title =
                                card.SelectedModel?.ShortDisplayName ?? Resources.Label_ExtraNetworks;
                            card.PropertyChanged -= ExtraNetworkCardOnPropertyChanged;
                            card.PropertyChanged += ExtraNetworkCardOnPropertyChanged;
                        }
                    }
                },
                DispatcherPriority.Background
            );
        }

        if (model.ExtraNetworksLow is not null)
        {
            ExtraNetworksLowStack.LoadStateFromJsonObject(model.ExtraNetworksLow);
            MigrateStackModulesToWan22(ExtraNetworksLowStack);

            Dispatcher.UIThread.Post(
                () =>
                {
                    foreach (var module in ExtraNetworksLowStack.Cards.OfType<Wan22LoraModule>())
                    {
                        module.TargetModelKey = "Low"; // ENSURE HERE
                        if (module.GetCard<ExtraNetworkCardViewModel>() is ExtraNetworkCardViewModel card)
                        {
                            module.Title =
                                card.SelectedModel?.ShortDisplayName ?? Resources.Label_ExtraNetworks;
                            card.PropertyChanged -= ExtraNetworkCardOnPropertyChanged;
                            card.PropertyChanged += ExtraNetworkCardOnPropertyChanged;
                        }
                    }
                },
                DispatcherPriority.Background
            );
        }
    }

    // Helper to migrate legacy LoraModule instances to Wan22LoraModule
    private void MigrateStackModulesToWan22(StackEditableCardViewModel stack)
    {
        if (stack is null)
            return;

        var targetKey =
            stack == ExtraNetworksHighStack ? "High"
            : stack == ExtraNetworksLowStack ? "Low"
            : null;

        for (int i = 0; i < stack.Cards.Count; i++)
        {
            // If a LoraModule (legacy) exists, migrate it to Wan22LoraModule
            if (stack.Cards[i] is LoraModule oldLora)
            {
                // Create new Wan22 module using vmFactory
                var newModule = (Wan22LoraModule)vmFactory.Get(typeof(Wan22LoraModule));

                // copy ExtraNetwork card properties if present
                if (
                    oldLora.GetCard<ExtraNetworkCardViewModel>() is ExtraNetworkCardViewModel oldCard
                    && newModule.GetCard<ExtraNetworkCardViewModel>() is ExtraNetworkCardViewModel newCard
                )
                {
                    newCard.SelectedModel = oldCard.SelectedModel;
                    newCard.ModelWeight = oldCard.ModelWeight;
                    newCard.ClipWeight = oldCard.ClipWeight;
                    newCard.IsClipWeightEnabled = oldCard.IsClipWeightEnabled;
                    newCard.IsModelWeightEnabled = oldCard.IsModelWeightEnabled;
                }

                // preserve title and enabled state
                newModule.Title = oldLora.Title;
                newModule.IsEnabled = oldLora.IsEnabled;

                // ensure the target is preserved based on which stack was migrated
                newModule.TargetModelKey = targetKey; // SET HERE

                // replace in stack
                stack.Cards[i] = newModule;
            }
        }
    }

    partial void OnSelectedModelChanged(HybridModelFile? value);

    partial void OnSelectedModelChanged(HybridModelFile? value)
    {
        // Update TabContext with the selected model
        tabContext.SelectedModel = value;

        // if (!IsExtraNetworksEnabled)
        //     return;

        foreach (var stack in new[] { ExtraNetworksHighStack, ExtraNetworksLowStack })
        {
            foreach (var card in stack.Cards)
            {
                if (card is not Wan22LoraModule loraModule)
                    continue;

                if (loraModule.GetCard<ExtraNetworkCardViewModel>() is not { } cardViewModel)
                    continue;

                cardViewModel.SelectedBaseModel = value;
            }
        }
    }

    internal class Wan22ModelModel
    {
        public string? SelectedModelHighName { get; init; }
        public string? SelectedModelLowName { get; init; }
        public string? SelectedVaeName { get; init; }
        public string? SelectedClipModelName { get; init; }
        public string? SelectedClipVisionModelName { get; init; }
        public string? SelectedDType { get; init; }
        public bool IsClipVisionEnabled { get; init; }
        public double Shift { get; init; }
        public JsonObject? ExtraNetworksHigh { get; init; }
        public JsonObject? ExtraNetworksLow { get; init; }
    }
}
