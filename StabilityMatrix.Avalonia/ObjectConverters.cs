using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia;

/// <summary>
/// Handy converters for dealing with null references in XAML bindings.
/// </summary>
public static class ObjectConverters
{
    /// <summary>
    /// Returns true when the bound value is null.
    /// </summary>
    public static FuncValueConverter<object?, bool> IsNull { get; } = new(value => value is null);

    /// <summary>
    /// Returns true when the bound value is not null.
    /// </summary>
    public static FuncValueConverter<object?, bool> IsNotNull { get; } = new(value => value is not null);
}
