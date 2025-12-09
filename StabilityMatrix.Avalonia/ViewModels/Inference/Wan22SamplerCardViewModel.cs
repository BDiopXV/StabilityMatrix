using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SamplerCard))]
[ManagedService]
[RegisterTransient<Wan22SamplerCardViewModel>]
public class Wan22SamplerCardViewModel : SamplerCardViewModel
{
    public List<string> OutputNodeNames { get; } = new();

    [JsonIgnore]
    public PromptWan22CardViewModel? PromptVm { get; set; }

    public Wan22SamplerCardViewModel(
        IInferenceClientManager clientManager,
        IServiceManager<ViewModelBase> vmFactory,
        ISettingsManager settingsManager,
        TabContext tabContext
    )
        : base(clientManager, vmFactory, settingsManager, tabContext)
    {
        EnableAddons = false;
        IsLengthEnabled = true;
        SelectedSampler = KJComfySampler.Euler.ToComfySampler();
        SelectedScheduler = ComfyScheduler.Simple;
        Length = 33;
    }

    public override void ApplyStep(ModuleApplyStepEventArgs e)
    {
        var startImage =
            e.Builder.Connections.Primary ?? throw new ValidationException("Missing input image");
        var vae = e.Builder.Connections.Base.VAE ?? throw new ValidationException("Missing VAE");
        var highModel =
            e.Builder.Connections.Models["High"].Model ?? throw new ValidationException("Missing High model");
        var lowModel =
            e.Builder.Connections.Models["Low"].Model ?? throw new ValidationException("Missing Low model");

        e.Builder.Connections.PrimaryCfg = CfgScale;

        // Image embeds
        var imageEmbeds = e.Builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.WanVideoImageToVideoEncode
            {
                Name = e.Builder.Nodes.GetUniqueName("WanVideoImageToVideoEncode"),
                Vae = vae,
                StartImage = (ImageNodeConnection)startImage,
                Width = Width,
                Height = Height,
                NumFrames = Length,
            }
        );

        // var (positiveCond, negativeCond) = e.Builder.Connections.Base.Conditioning
        //     ?? throw new ValidationException("Missing conditioning");

        // High sampler
        var samplerHigh = e.Builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.WanVideoSampler
            {
                Name = e.Builder.Nodes.GetUniqueName("WanVideoSampler_High"),
                Model = highModel,
                ImageEmbeds = imageEmbeds.Output,
                TextEmbeds = PromptVm.textEmbeds.Output,
                Steps = Steps,
                Cfg = (double)e.Builder.Connections.PrimaryCfg,
                StartStep = 0,
                EndStep = (int)(Steps / 2),
                Shift = (double)e.Builder.Connections.PrimaryShift, // Add appropriate value
                Seed = e.Builder.Connections.Seed,
                Scheduler = selectedKJSampler.Name,
                ForceOffload = true,
            }
        );

        // Low sampler (chained latent)
        var samplerLow = e.Builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.WanVideoSampler
            {
                Name = e.Builder.Nodes.GetUniqueName("WanVideoSampler_Low"),
                Model = lowModel,
                ImageEmbeds = imageEmbeds.Output,
                TextEmbeds = PromptVm.textEmbeds.Output,
                Samples = samplerHigh.Output1,
                // DenoisedSamples = samplerHigh.Output2,
                Steps = Steps,
                Cfg = (double)e.Builder.Connections.PrimaryCfg,
                StartStep = (int)(Steps / 2),
                EndStep = -1,
                Shift = (double)e.Builder.Connections.PrimaryShift, // Add appropriate value
                RiflexFreqIndex = 0,
                Seed = e.Builder.Connections.Seed + 158,
                Scheduler = selectedKJSampler.Name,
                ForceOffload = true,
            }
        );

        // Decode
        var decode = e.Builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.WanVideoDecode
            {
                Name = e.Builder.Nodes.GetUniqueName("WanVideoDecode"),
                Vae = vae,
                Samples = samplerLow.Output2, // Use the denoised_samples output
            }
        );

        // Upscale
        var upscaleModel = e.Builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.UpscaleModelLoader(
                e.Builder.Nodes.GetUniqueName("UpscaleModelLoader"),
                "4x-ClearRealityV1_Soft.pth"
            )
        );

        var upscaled = e.Builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.ImageUpscaleWithModel(
                e.Builder.Nodes.GetUniqueName("ImageUpscaleWithModel"),
                upscaleModel.Output,
                decode.Output
            )
        );

        // Resize
        var resized = e.Builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.ImageResizeKJv2
            {
                Name = e.Builder.Nodes.GetUniqueName("ImageResizeKJv2"),
                Image = upscaled.Output,
                Width = 720,
                Height = 10000,
            }
        );

        // RIFE interpolation
        var rife = e.Builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.RIFEVFI
            {
                Name = e.Builder.Nodes.GetUniqueName("RIFE"),
                Frames = resized.Output,
                ClearCacheAfterNFrames = 16,
                CkptName = "rife49.pth",
                Multiplier = 4,
                FastMode = false,
                Ensemble = true,
                ScaleFactor = 1,
            }
        );

        // Store the rife output for later use in output settings
        e.Builder.Connections.RifeVideoOutput = rife.Output;
    }
}
