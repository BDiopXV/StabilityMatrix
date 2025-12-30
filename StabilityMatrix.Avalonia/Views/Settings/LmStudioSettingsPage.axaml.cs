using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Settings;

[RegisterSingleton<LmStudioSettingsPage>]
public partial class LmStudioSettingsPage : UserControlBase
{
    public LmStudioSettingsPage()
    {
        InitializeComponent();
    }
}
