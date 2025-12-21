using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.Converters;

public class FileUriConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (targetType != typeof(Uri))
        {
            return null;
        }

        return value switch
        {
            string str when str.StartsWith("avares://") => new Uri(str),
            string str when (str.StartsWith("https://") || str.StartsWith("http://")) => new Uri(str),
            string str => new Uri("file://" + ResolveVideoPreviewPath(str)),
            IFormattable formattable => new Uri(
                "file://" + ResolveVideoPreviewPath(formattable.ToString(null, culture))
            ),
            _ => null,
        };
    }

    private static string ResolveVideoPreviewPath(string path)
    {
        try
        {
            if (!ImageMetadata.IsVideoExtension(Path.GetExtension(path)))
            {
                return path;
            }

            var sidecarPreviewPath = Path.ChangeExtension(path, ".png");
            return File.Exists(sidecarPreviewPath) ? sidecarPreviewPath : path;
        }
        catch
        {
            return path;
        }
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (targetType == typeof(string) && value is Uri uri)
        {
            return uri.ToString().StripStart("file://");
        }

        return null;
    }
}
