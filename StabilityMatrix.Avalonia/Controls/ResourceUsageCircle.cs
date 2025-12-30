using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// A circular progress indicator for resource usage display
/// </summary>
public class ResourceUsageCircle : TemplatedControl
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<
        ResourceUsageCircle,
        double
    >(nameof(Value), 0);

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<
        ResourceUsageCircle,
        double
    >(nameof(Maximum), 100);

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty = AvaloniaProperty.Register<
        ResourceUsageCircle,
        IBrush?
    >(nameof(IndicatorBrush));

    public IBrush? IndicatorBrush
    {
        get => GetValue(IndicatorBrushProperty);
        set => SetValue(IndicatorBrushProperty, value);
    }

    public static readonly StyledProperty<IBrush?> TrackBrushProperty = AvaloniaProperty.Register<
        ResourceUsageCircle,
        IBrush?
    >(nameof(TrackBrush));

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<
        ResourceUsageCircle,
        double
    >(nameof(StrokeThickness), 4);

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<
        ResourceUsageCircle,
        string?
    >(nameof(Label));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly StyledProperty<string?> ValueTextProperty = AvaloniaProperty.Register<
        ResourceUsageCircle,
        string?
    >(nameof(ValueText));

    public string? ValueText
    {
        get => GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    /// <summary>
    /// Gets the sweep angle calculated from Value and Maximum
    /// </summary>
    public double SweepAngle => Maximum > 0 ? (Value / Maximum) * 360 : 0;

    static ResourceUsageCircle()
    {
        ValueProperty.Changed.AddClassHandler<ResourceUsageCircle>((x, _) => x.InvalidateVisual());
        MaximumProperty.Changed.AddClassHandler<ResourceUsageCircle>((x, _) => x.InvalidateVisual());
    }
}
