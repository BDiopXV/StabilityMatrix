using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls.Dock;

namespace StabilityMatrix.Avalonia.Views.Inference;

[RegisterTransient<InferenceWan22ImageToVideoView>]
public partial class InferenceWan22ImageToVideoView : DockUserControlBase
{
    public InferenceWan22ImageToVideoView()
    {
        InitializeComponent();
    }
}
