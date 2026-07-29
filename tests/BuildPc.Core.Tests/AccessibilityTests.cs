using System.Xml;
using System.Xml.Linq;

namespace BuildPc.Core.Tests;

/// <summary>
/// Um botão que só mostra um ícone não tem texto para um leitor de tela
/// anunciar. Sem <c>AutomationProperties.Name</c> ele é lido como "botão", sem
/// dizer o que faz.
/// </summary>
public sealed class AccessibilityTests
{
    [Fact]
    public void IconOnlyButtonsHaveAnAccessibleName()
    {
        var unnamed = EnumerateViews()
            .SelectMany(FindIconOnlyButtonsWithoutName)
            .ToList();

        Assert.True(
            unnamed.Count == 0,
            "Botões que só exibem ícone precisam de AutomationProperties.Name " +
            "para serem anunciados por um leitor de tela: " +
            string.Join(", ", unnamed));
    }

    [Fact]
    public void EveryViewDeclaresAtLeastOneAccessibleName()
    {
        // Guarda contra uma tela nova nascer sem nenhum rótulo acessível.
        var withoutAny = EnumerateViews()
            .Where(path => !File
                .ReadAllText(path)
                .Contains("AutomationProperties.Name", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .Where(name => !string.Equals(
                name,
                "StartupErrorWindow.axaml",
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            withoutAny.Count == 0,
            "Telas sem nenhum nome acessível declarado: " +
            string.Join(", ", withoutAny));
    }

    private static IEnumerable<string> EnumerateViews() =>
        Directory.EnumerateFiles(
            Path.Combine(FindRepositoryRoot(), "src", "BuildPc.Desktop", "Views"),
            "*.axaml",
            SearchOption.AllDirectories);

    private static IEnumerable<string> FindIconOnlyButtonsWithoutName(string path)
    {
        var document = XDocument.Load(path, LoadOptions.SetLineInfo);
        foreach (var button in document
                     .Descendants()
                     .Where(element => element.Name.LocalName == "Button"))
        {
            if (HasAccessibleName(button) || !IsIconOnly(button))
            {
                continue;
            }

            var lineInfo = (IXmlLineInfo)button;
            yield return
                $"{Path.GetFileName(path)}:{lineInfo.LineNumber}";
        }
    }

    private static bool HasAccessibleName(XElement button) =>
        button
            .Attributes()
            .Any(attribute =>
                attribute.Name.LocalName == "Name" &&
                attribute.Name.NamespaceName.Contains(
                    "Automation",
                    StringComparison.OrdinalIgnoreCase)) ||
        button
            .Attributes()
            .Any(attribute => attribute.Name.LocalName is
                "AutomationProperties.Name" or "Name" &&
                attribute.Name.NamespaceName.Length == 0 &&
                attribute.Name.LocalName == "AutomationProperties.Name");

    /// <summary>
    /// Um botão é considerado apenas ícone quando não há nenhum
    /// <c>TextBlock</c> dentro dele nem texto direto no conteúdo.
    /// </summary>
    private static bool IsIconOnly(XElement button)
    {
        var hasIcon = button
            .Descendants()
            .Any(element => element.Name.LocalName == "PathIcon");
        if (!hasIcon)
        {
            return false;
        }

        var hasText = button
            .Descendants()
            .Any(element => element.Name.LocalName == "TextBlock");
        var hasContentAttribute = button
            .Attributes()
            .Any(attribute => attribute.Name.LocalName == "Content");
        var hasInlineText = button.Nodes().OfType<XText>()
            .Any(text => !string.IsNullOrWhiteSpace(text.Value));

        return !hasText && !hasContentAttribute && !hasInlineText;
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[]
                 {
                     Directory.GetCurrentDirectory(),
                     AppContext.BaseDirectory
                 })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "BuildPc.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Não foi possível localizar a raiz do repositório BuildPC.");
    }
}
