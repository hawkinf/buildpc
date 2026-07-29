using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace BuildPc.Desktop.Converters;

public sealed class IconResourceConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is string iconKey &&
            Application.Current is { } application &&
            application.TryFindResource($"Icon.{iconKey}", out var resource) &&
            resource is not null)
        {
            return resource;
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        throw new NotSupportedException();
}
