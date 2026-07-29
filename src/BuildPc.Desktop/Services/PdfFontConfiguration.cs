using PdfSharp.Fonts;

namespace BuildPc.Desktop.Services;

internal static class PdfFontConfiguration
{
    private static readonly object SyncRoot = new();
    private static bool _isConfigured;

    public static void EnsureConfigured()
    {
        lock (SyncRoot)
        {
            if (_isConfigured)
            {
                return;
            }

            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            _isConfigured = true;
        }
    }
}
