using System.Xml;
using System.Xml.Linq;

namespace BuildPc.Core.Tests;

public sealed class ScrollableLayoutTests
{
    [Fact]
    public void ScrollViewerUsesNaturallySizedDirectContent()
    {
        var desktopDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BuildPc.Desktop");
        var invalidLayouts = Directory
            .EnumerateFiles(desktopDirectory, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(FindInvalidScrollableGrids)
            .ToList();

        Assert.True(
            invalidLayouts.Count == 0,
            "Grids diretamente dentro de ScrollViewer podem limitar a extensão " +
            "rolável e cortar conteúdo. Use StackPanel ou ItemsControl: " +
            string.Join(", ", invalidLayouts));
    }

    [Fact]
    public void ScrollSafeAreaComesFromTheSharedStyle()
    {
        var desktopDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BuildPc.Desktop");
        var hardCodedSpacers = Directory
            .EnumerateFiles(
                Path.Combine(desktopDirectory, "Views"),
                "*.axaml",
                SearchOption.AllDirectories)
            .SelectMany(FindHardCodedSafeAreaSpacers)
            .ToList();

        Assert.True(
            hardCodedSpacers.Count == 0,
            "A folga inferior das telas roláveis deve usar " +
            "Classes=\"scroll-safe-area\", para o valor não divergir entre " +
            "telas: " + string.Join(", ", hardCodedSpacers));
    }

    private static IEnumerable<string> FindHardCodedSafeAreaSpacers(string path)
    {
        var document = XDocument.Load(path, LoadOptions.SetLineInfo);
        foreach (var border in document
                     .Descendants()
                     .Where(element => element.Name.LocalName == "Border"))
        {
            // Um Border vazio com altura fixa alta só existe como folga de
            // rolagem; separadores finos e cartões com conteúdo não contam.
            if (border.HasElements ||
                border.Attribute("Height") is not { } height ||
                !double.TryParse(
                    height.Value,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var value) ||
                value < 100)
            {
                continue;
            }

            var lineInfo = (IXmlLineInfo)border;
            yield return
                $"{Path.GetRelativePath(FindRepositoryRoot(), path)}:{lineInfo.LineNumber}";
        }
    }

    private static IEnumerable<string> FindInvalidScrollableGrids(string path)
    {
        var document = XDocument.Load(path, LoadOptions.SetLineInfo);
        foreach (var scrollViewer in document
                     .Descendants()
                     .Where(element => element.Name.LocalName == "ScrollViewer"))
        {
            var content = scrollViewer
                .Elements()
                .FirstOrDefault(element =>
                    !element.Name.LocalName.Contains('.', StringComparison.Ordinal));
            if (content?.Name.LocalName != "Grid")
            {
                continue;
            }

            var lineInfo = (IXmlLineInfo)content;
            yield return
                $"{Path.GetRelativePath(FindRepositoryRoot(), path)}:{lineInfo.LineNumber}";
        }
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
