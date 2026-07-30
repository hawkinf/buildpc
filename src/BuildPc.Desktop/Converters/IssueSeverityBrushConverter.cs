using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using BuildPc.Core.Models;

namespace BuildPc.Desktop.Converters;

public sealed class IssueSeverityBrushConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        var key = value is IssueSeverity.Error
            ? "Brush.Error"
            : "Brush.Warning";
        if (Application.Current is { } application &&
            application.TryFindResource(key, out var resource) &&
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
