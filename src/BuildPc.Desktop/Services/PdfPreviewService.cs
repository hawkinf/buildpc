using System.Text.RegularExpressions;

namespace BuildPc.Desktop.Services;

public static class PdfPreviewService
{
    private static readonly string PreviewDirectory = Path.Combine(
        Path.GetTempPath(),
        "BuildPC",
        "visualizacoes-pdf");

    // Path.GetInvalidFileNameChars() varia por sistema operacional (no Linux
    // só bloqueia '/' e NUL) — a lista fixa abaixo garante a mesma
    // sanitização em qualquer plataforma, independente de onde o preview é
    // gerado.
    private static readonly char[] InvalidFileNameChars = Path
        .GetInvalidFileNameChars()
        .Concat(['\\', '/', ':', '*', '?', '"', '<', '>', '|'])
        .Distinct()
        .ToArray();

    public static string CreatePath(string suggestedFileName)
    {
        Directory.CreateDirectory(PreviewDirectory);
        CleanupOldPreviews();

        var requestedName = Path.GetFileNameWithoutExtension(
            Path.GetFileName(suggestedFileName));
        var safeName = string.Concat(
            requestedName.Select(character =>
                InvalidFileNameChars.Contains(character)
                    ? '_'
                    : character));
        safeName = Regex.Replace(safeName, @"\.{2,}", "_");
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
