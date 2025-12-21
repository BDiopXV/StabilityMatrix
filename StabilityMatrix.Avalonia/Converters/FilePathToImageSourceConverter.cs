using System;
using Avalonia.Data.Converters;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Builds an ImageSource from a file path for thumbnail controls that expect ImageSource.
/// </summary>
public sealed class FilePathToImageSourceConverter : IValueConverter
{
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        System.Globalization.CultureInfo culture
    )
    {
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            return new ImageSource(new FilePath(str));
        }

        return null;
    }

    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        System.Globalization.CultureInfo culture
    ) => throw new NotSupportedException();
}
