using PdfSharp.Fonts;

namespace BuildPc.Core.Services;

/// <summary>
/// Configura de onde o PdfSharp lê a fonte usada nos PDFs (orçamento e
/// tabela de preços).
/// </summary>
/// <remarks>
/// No Windows, o PdfSharp já sabe ler "Arial" das fontes instaladas do
/// sistema. No Linux (VPS, e qualquer publicação do cliente web) não existe
/// "Arial" instalada, então é preciso um <see cref="IFontResolver"/> que
/// aponte para uma fonte de fato presente no servidor.
/// </remarks>
public static class PdfFontConfiguration
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

            if (OperatingSystem.IsWindows())
            {
                GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            }
            else
            {
                GlobalFontSettings.FontResolver = new LinuxFontResolver();
            }

            _isConfigured = true;
        }
    }
}

/// <summary>
/// Resolve "Arial" para a DejaVu Sans instalada de fábrica na maioria das
/// distribuições Debian/Ubuntu (pacote <c>fonts-dejavu-core</c>) — já
/// confirmada presente na VPS de produção. Métrica bem próxima da Arial, o
/// suficiente para o layout do PDF não quebrar.
/// </summary>
internal sealed class LinuxFontResolver : IFontResolver
{
    private const string FamilyName = "Arial";

    private static readonly string RegularPath =
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";

    private static readonly string BoldPath =
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf";

    public byte[] GetFont(string faceName)
    {
        var path = faceName.Contains("bold", StringComparison.OrdinalIgnoreCase)
            ? BoldPath
            : RegularPath;
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Fonte não encontrada em '{path}'. Instale o pacote " +
                "'fonts-dejavu-core' (Debian/Ubuntu) no servidor.");
        }

        return File.ReadAllBytes(path);
    }

    public FontResolverInfo ResolveTypeface(
        string familyName,
        bool isBold,
        bool isItalic) =>
        new(FamilyName, isBold, isItalic);
}
