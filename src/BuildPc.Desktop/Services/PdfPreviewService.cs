namespace BuildPc.Desktop.Services;

public static class PdfPreviewService
{
    private static readonly string PreviewDirectory = Path.Combine(
        Path.GetTempPath(),
        "BuildPC",
        "visualizacoes-pdf");

    public static string CreatePath(string suggestedFileName)
    {
        Directory.CreateDirectory(PreviewDirectory);
        CleanupOldPreviews();

        var requestedName = Path.GetFileNameWithoutExtension(
            Path.GetFileName(suggestedFileName));
        var safeName = string.Concat(
            requestedName.Select(character =>
                Path.GetInvalidFileNameChars().Contains(character)
                    ? '_'
                    : character));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "documento";
        }

        return Path.Combine(
            PreviewDirectory,
            $"{safeName}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.pdf");
    }

    private static void CleanupOldPreviews()
    {
        try
        {
            // As prévias incluem tabelas de custo e dados de clientes. Guardá-las
            // por dois dias em uma pasta temporária sem proteção era exposição
            // desnecessária: uma hora cobre a visualização e a impressão.
            var expiration = DateTime.UtcNow.AddHours(-1);
            foreach (var path in Directory.EnumerateFiles(
                         PreviewDirectory,
                         "*.pdf",
                         SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(path) < expiration)
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                    // Uma prévia aberta ou bloqueada será revisitada depois.
                }
            }
        }
        catch
        {
            // A limpeza é auxiliar e não deve impedir uma nova visualização.
        }
    }
}
