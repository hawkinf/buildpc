using System.Diagnostics;

namespace BuildPc.Desktop.Services;

public static class SystemFileLauncher
{
    public static bool Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.GetFullPath(path),
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
