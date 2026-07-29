using BuildPc.Desktop.Services;

namespace BuildPc.Core.Tests;

public sealed class PdfPreviewServiceTests
{
    [Fact]
    public void CreatePath_ReturnsUniqueSafePdfPaths()
    {
        var first = PdfPreviewService.CreatePath(@"..\orçamento: cliente?.pdf");
        var second = PdfPreviewService.CreatePath(@"..\orçamento: cliente?.pdf");

        Assert.Equal(".pdf", Path.GetExtension(first));
        Assert.Equal(".pdf", Path.GetExtension(second));
        Assert.NotEqual(first, second);
        Assert.DoesNotContain("..", Path.GetFileName(first));
        Assert.Equal(Path.GetDirectoryName(first), Path.GetDirectoryName(second));
    }
}
