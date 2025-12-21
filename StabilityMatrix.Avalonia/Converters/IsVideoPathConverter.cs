using System;
using Avalonia.Data.Converters;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Returns true when a file path has a known video extension.
/// </summary>
public sealed class IsVideoPathConverter : IValueConverter
{
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        System.Globalization.CultureInfo culture
    )
    {
        var path = value as string;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = System.IO.Path.GetExtension(path);
        return ImageMetadata.IsVideoExtension(extension);
    }

    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        System.Globalization.CultureInfo culture
    ) => throw new NotSupportedException();
}
