using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterTransient<Wan22LoraModule>]
public partial class Wan22LoraModule : ModuleBase
{
    /// <summary>
    /// Determines which model within the builder this module targets ("High" or "Low")
    /// </summary>
    public string? TargetModelKey { get; set; }

    /// <inheritdoc />
    public override bool IsSettingsEnabled => true;

    /// <inheritdoc />
    public override IRelayCommand SettingsCommand => OpenSettingsDialogCommand;

    public Wan22LoraModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "LoRA";

        var extraNetworksVm = vmFactory.Get<ExtraNetworkCardViewModel>(card =>
        {
            card.IsModelWeightEnabled = true;
            card.IsClipWeightToggleEnabled = true;
            card.IsClipWeightEnabled = false;
        });

        AddCards(extraNetworksVm);

        AddDisposable(
            extraNetworksVm
                .WhenPropertyChanged(vm => vm.SelectedModel)
                .Throttle(TimeSpan.FromMilliseconds(50))
                .Subscribe(next =>
                {
                    var model = next.Value;
                    if (model is null)
                    {
                        Title = Resources.Label_ExtraNetworks;
                        return;
                    }

                    if (model.Local?.HasConnectedModel ?? false)
                    {
                        Title = model.Local.ConnectedModelInfo.ModelName;
                    }
                    else
                    {
                        Title = model.ShortDisplayName;
                    }
                })
        );
    }

    [RelayCommand]
    private async Task OpenSettingsDialog()
    {
        var gridVm = VmFactory.Get<PropertyGridViewModel>(vm =>
        {
            vm.Title = $"{Title} {Resources.Label_Settings}";
            vm.SelectedObject = Cards.ToArray();
            vm.IncludeCategories = new[] { "Settings" };
        });

        await gridVm.GetDialog().ShowAsync();
    }

    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        // Don't apply here - parent will chain and apply LoRAs
        // This module just holds the LoRA selection
    }
}
