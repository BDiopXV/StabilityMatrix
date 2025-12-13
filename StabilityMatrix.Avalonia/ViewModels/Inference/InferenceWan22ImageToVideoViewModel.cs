using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.ViewModels.Inference.Video;
using StabilityMatrix.Avalonia.Views.Inference;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceWan22ImageToVideoView), IsPersistent = true)]
[RegisterScoped<InferenceWan22ImageToVideoViewModel>, ManagedService]
public class InferenceWan22ImageToVideoViewModel : InferenceGenerationViewModelBase, IParametersLoadableState
{
    protected override bool ShouldEnableVideoPlayer => true;

    [JsonIgnore]
    public StackCardViewModel StackCardViewModel { get; }

    [JsonPropertyName("Model")]
    public Wan22ModelCardViewModel ModelCardViewModel { get; }

    [JsonPropertyName("Sampler")]
    public Wan22SamplerCardViewModel SamplerCardViewModel { get; }

    [JsonPropertyName("BatchSize")]
    public BatchSizeCardViewModel BatchSizeCardViewModel { get; }

    [JsonPropertyName("Seed")]
    public SeedCardViewModel SeedCardViewModel { get; }

    [JsonPropertyName("Prompt")]
    public PromptWan22CardViewModel PromptCardViewModel { get; }

    [JsonPropertyName("VideoOutput")]
    public VideoWan22OutputSettingsCardViewModel VideoOutputSettingsCardViewModel { get; }

    [JsonPropertyName("ImageLoader")]
    public SelectImageCardViewModel SelectImageCardViewModel { get; }

    public InferenceWan22ImageToVideoViewModel(
        IServiceManager<ViewModelBase> vmFactory,
        IInferenceClientManager inferenceClientManager,
        INotificationService notificationService,
        ISettingsManager settingsManager,
        RunningPackageService runningPackageService
    )
        : base(vmFactory, inferenceClientManager, notificationService, settingsManager, runningPackageService)
    {
        SeedCardViewModel = vmFactory.Get<SeedCardViewModel>();
        SeedCardViewModel.GenerateNewSeed();

        ModelCardViewModel = vmFactory.Get<Wan22ModelCardViewModel>();

        SamplerCardViewModel = vmFactory.Get<Wan22SamplerCardViewModel>(samplerCard =>
        {
            samplerCard.IsDimensionsEnabled = true;
            samplerCard.IsCfgScaleEnabled = true;
            samplerCard.IsSamplerSelectionEnabled = true;
            samplerCard.IsSchedulerSelectionEnabled = true;
            samplerCard.DenoiseStrength = 1.0d;
            samplerCard.EnableAddons = false;
            samplerCard.IsLengthEnabled = true;
            samplerCard.Width = 832;
            samplerCard.Height = 480;
            samplerCard.Length = 33;
        });

        PromptCardViewModel = AddDisposable(vmFactory.Get<PromptWan22CardViewModel>());

        BatchSizeCardViewModel = vmFactory.Get<BatchSizeCardViewModel>();

        VideoOutputSettingsCardViewModel = vmFactory.Get<VideoWan22OutputSettingsCardViewModel>(vm =>
            vm.Fps = 16.0d
        );

        StackCardViewModel = vmFactory.Get<StackCardViewModel>();
        StackCardViewModel.AddCards(
            ModelCardViewModel,
            SamplerCardViewModel,
            SeedCardViewModel,
            BatchSizeCardViewModel,
            VideoOutputSettingsCardViewModel
        );

        SelectImageCardViewModel = vmFactory.Get<SelectImageCardViewModel>();

        SamplerCardViewModel.IsDenoiseStrengthEnabled = true;

        ModelCardViewModel.IsClipVisionEnabled = true;
    }

    /// <inheritdoc />
    protected override void BuildPrompt(BuildPromptEventArgs args)
    {
        base.BuildPrompt(args);

        var builder = args.Builder;

        builder.Connections.Seed = args.SeedOverride switch
        {
            { } seed => Convert.ToUInt64(seed),
            _ => Convert.ToUInt64(SeedCardViewModel.Seed),
        };

        // Load models
        ModelCardViewModel.ApplyStep(args);

        // Setup latent from image
        var imageLoad = builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.LoadImage
            {
                Name = builder.Nodes.GetUniqueName("ControlNet_LoadImage"),
                Image =
                    SelectImageCardViewModel.ImageSource?.GetHashGuidFileNameCached("Inference")
                    ?? throw new ValidationException(),
            }
        );

        var imageResizePlus = builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.ImageResizePlus
            {
                Image = imageLoad.Output1,
                Name = builder.Nodes.GetUniqueName("ImageResizePlus"),
                Width = SamplerCardViewModel.Width,
                Height = SamplerCardViewModel.Height,
                Interpolation = "lanczos",
                Method = "keep proportion",
                Condition = "always",
                MultipleOf = 32,
            }
        );

        builder.Connections.Primary = imageResizePlus.Output1;
        builder.Connections.PrimarySize = SelectImageCardViewModel.CurrentBitmapSize;

        BatchSizeCardViewModel.ApplyStep(args);

        // SelectImageCardViewModel.ApplyStep(args);

        PromptCardViewModel.SelectedClipModelFile = ModelCardViewModel.SelectedClipModel;

        PromptCardViewModel.ApplyStep(args);

        SamplerCardViewModel.PromptVm = PromptCardViewModel;

        SamplerCardViewModel.ApplyStep(args);

        // Animated webp output
        VideoOutputSettingsCardViewModel.ApplyStep(args);
    }

    protected override async Task GenerateImageImpl(
        GenerateOverrides overrides,
        CancellationToken cancellationToken
    )
    {
        if (!await CheckClientConnectedWithPrompt() || !ClientManager.IsConnected)
        {
            return;
        }

        if (!await ModelCardViewModel.ValidateModel())
            return;

        // If enabled, randomize the seed
        var seedCard = StackCardViewModel.GetCard<SeedCardViewModel>();
        if (overrides is not { UseCurrentSeed: true } && seedCard.IsRandomizeEnabled)
        {
            seedCard.GenerateNewSeed();
        }

        var batches = BatchSizeCardViewModel.BatchCount;

        var batchArgs = new List<ImageGenerationEventArgs>();

        for (var i = 0; i < batches; i++)
        {
            var seed = seedCard.Seed + i;

            var buildPromptArgs = new BuildPromptEventArgs { Overrides = overrides, SeedOverride = seed };
            BuildPrompt(buildPromptArgs);

            // update seed in project for batches
            var inferenceProject = InferenceProjectDocument.FromLoadable(this);
            if (inferenceProject.State?["Seed"]?["Seed"] is not null)
            {
                inferenceProject = inferenceProject.WithState(x => x["Seed"]["Seed"] = seed);
            }

            var generationArgs = new ImageGenerationEventArgs
            {
                Client = ClientManager.Client,
                Nodes = buildPromptArgs.Builder.ToNodeDictionary(),
                OutputNodeNames = buildPromptArgs.Builder.Connections.OutputNodeNames.ToArray(),
                Parameters = SaveStateToParameters(new GenerationParameters()) with
                {
                    Seed = Convert.ToUInt64(seed),
                },
                Project = inferenceProject,
                FilesToTransfer = buildPromptArgs.FilesToTransfer,
                BatchIndex = i,
                // Only clear output images on the first batch
                ClearOutputImages = i == 0,
            };

            batchArgs.Add(generationArgs);
        }

        // Run batches
        foreach (var args in batchArgs)
        {
            await RunGeneration(args, cancellationToken);
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<ImageSource> GetInputImages()
    {
        if (SelectImageCardViewModel.ImageSource is { } image)
        {
            yield return image;
        }
    }

    /// <inheritdoc />
    public void LoadStateFromParameters(GenerationParameters parameters)
    {
        SamplerCardViewModel.LoadStateFromParameters(parameters);
        ModelCardViewModel.LoadStateFromParameters(parameters);
        PromptCardViewModel.LoadStateFromParameters(parameters);
        VideoOutputSettingsCardViewModel.LoadStateFromParameters(parameters);
        SeedCardViewModel.Seed = Convert.ToInt64(parameters.Seed);
    }

    /// <inheritdoc />
    public GenerationParameters SaveStateToParameters(GenerationParameters parameters)
    {
        parameters = SamplerCardViewModel.SaveStateToParameters(parameters);
        parameters = ModelCardViewModel.SaveStateToParameters(parameters);
        parameters = PromptCardViewModel.SaveStateToParameters(parameters);
        parameters = VideoOutputSettingsCardViewModel.SaveStateToParameters(parameters);

        parameters.Seed = (ulong)SeedCardViewModel.Seed;

        return parameters;
    }
}
