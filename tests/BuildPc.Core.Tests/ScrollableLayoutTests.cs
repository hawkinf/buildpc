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
