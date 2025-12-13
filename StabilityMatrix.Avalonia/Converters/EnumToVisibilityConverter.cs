using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Converts an enum value to a boolean based on equality with the parameter.
/// Returns true if value equals parameter, false otherwise.
/// The parameter can be a string representation of the enum value.
/// </summary>
public class EnumToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        var valueType = value.GetType();

        // If parameter is a string, try to parse it as the same enum type
        if (parameter is string parameterString && valueType.IsEnum)
        {
            try
            {
                var parsedParameter = Enum.Parse(valueType, parameterString);
                return value.Equals(parsedParameter);
            }
            catch
            {
                return false;
            }
        }

        // If types match, compare directly
        if (valueType == parameter.GetType())
        {
            return value.Equals(parameter);
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
