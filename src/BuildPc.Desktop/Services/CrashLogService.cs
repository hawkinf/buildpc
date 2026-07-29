namespace BuildPc.Desktop.Services;

/// <summary>
/// Registra falhas não tratadas em um arquivo local. O usuário nunca vê o
/// texto técnico; ele fica disponível apenas para diagnóstico posterior.
/// </summary>
public static class CrashLogService
{
    private static readonly object WriteLock = new();

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BuildPC",
        "erros.log");

    public static void Record(string origin, Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            lock (WriteLock)
            {
                TrimIfTooLarge();
                File.AppendAllText(
                    LogPath,
                    $"""

                    ===== {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} • {origin} =====
                    {exception}

                    """);
            }
        }
        catch
        {
            // Registrar a falha nunca pode provocar outra falha.
        }
    }

    private static void TrimIfTooLarge()
    {
        const long maximumBytes = 1024 * 1024;
        var info = new FileInfo(LogPath);
        if (info.Exists && info.Length > maximumBytes)
        {
            File.Delete(LogPath);
        }
    }
}
