using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool @bool)
        {
            return !@bool;
        }

        return true; // default to showing the play icon when the state is unknown
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
