using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace BuildPc.Desktop.Converters;

public sealed class SubtractConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is double number &&
            double.TryParse(
                parameter?.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            return Math.Max(0, number - amount);
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
