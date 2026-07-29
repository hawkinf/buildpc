using Avalonia;
using Avalonia.Styling;
using BuildPc.Core.Models;

namespace BuildPc.Desktop.Services;

public static class ApplicationThemeService
{
    public static void Apply(AppThemeMode mode)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = mode switch
        {
            AppThemeMode.Light => ThemeVariant.Light,
            AppThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
