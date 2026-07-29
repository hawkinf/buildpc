using BuildPc.Core.Models;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SkiaSharp;

namespace BuildPc.Desktop.Services;

public sealed record ProductPriceTableRow(
    string Title,
    string? ImageUrl,
    decimal Price,
    ComponentCategory Category = ComponentCategory.Processor,
    string CategoryName = "");

public sealed record ProductPriceTableSection(
    string? CategoryName,
    IReadOnlyList<ProductPriceTableRow> Rows);

public sealed record ProductPriceTableDocument(
    string Title,
    string PriceColumnTitle,
    string Scope,
    string FilterText,
    string CompanyName,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ProductPriceTableRow> Rows,
    bool GroupByCategory = false);

public sealed class ProductPriceTablePdfService
{
    private const int MaximumImageBytes = 5 * 1024 * 1024;
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task ExportAsync(
        ProductPriceTableDocument priceTable,
        string outputPath)
    {
        ArgumentNullException.ThrowIfNull(priceTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        PdfFontConfiguration.EnsureConfigured();

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-tabela-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var sections = ProductPriceTableSectionFactory.Create(priceTable);
            var orderedRows = sections.SelectMany(section => section.Rows).ToList();
            var imagePaths = await PrepareImagesAsync(
                orderedRows,
                temporaryDirectory);
            await Task.Run(() => Render(
                priceTable,
                sections,
                imagePaths,
                outputPath));
        }
        finally
        {
            try
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
            catch
            {
                // A exportação já foi concluída; um arquivo temporário bloqueado
                // não deve impedir a entrega do PDF.
            }
        }
    }

    private static void Render(
        ProductPriceTableDocument priceTable,
        IReadOnlyList<ProductPriceTableSection> productSections,
        IReadOnlyList<string?> imagePaths,
        string outputPath)
    {
        var document = BuildDocument(priceTable, productSections, imagePaths);
        var renderer = new PdfDocumentRenderer
        {
            Document = document
        };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(outputPath);
    }

    private static Document BuildDocument(
        ProductPriceTableDocument priceTable,
        IReadOnlyList<ProductPriceTableSection> productSections,
        IReadOnlyList<string?> imagePaths)
    {
        var document = new Document
        {
            Info =
            {
                Title = priceTable.Title,
                Author = string.IsNullOrWhiteSpace(priceTable.CompanyName)
                    ? "BuildPC"
                    : priceTable.CompanyName
            }
        };
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 9;
        normal.Font.Color = Color.FromRgb(35, 43, 55);

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.3);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.3);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.5);

        if (!string.IsNullOrWhiteSpace(priceTable.CompanyName))
        {
            var company = section.AddParagraph(priceTable.CompanyName);
            company.Format.Font.Size = 11;
            company.Format.Font.Bold = true;
            company.Format.Font.Color = Color.FromRgb(43, 112, 219);
        }

        var title = section.AddParagraph(priceTable.Title.ToUpperInvariant());
        title.Format.Font.Size = 19;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Color.FromRgb(22, 55, 93);
        title.Format.SpaceAfter = Unit.FromMillimeter(2);

        var details = section.AddParagraph();
        details.Format.Font.Size = 8;
        details.Format.Font.Color = Color.FromRgb(91, 103, 117);
        details.AddText($"Categoria: {priceTable.Scope}");
        details.AddLineBreak();
        details.AddText(string.IsNullOrWhiteSpace(priceTable.FilterText)
            ? "Filtro: nenhum"
            : $"Filtro: {priceTable.FilterText}");
        details.AddLineBreak();
        details.AddText(
            $"Produtos: {priceTable.Rows.Count} • Gerado em " +
            $"{priceTable.GeneratedAt.LocalDateTime:dd/MM/yyyy 'às' HH:mm}");
        details.Format.SpaceAfter = Unit.FromMillimeter(4);

        var imageIndex = 0;
        for (var sectionIndex = 0;
             sectionIndex < productSections.Count;
             sectionIndex++)
        {
            var productSection = productSections[sectionIndex];
            if (!string.IsNullOrWhiteSpace(productSection.CategoryName))
            {
                var category = section.AddParagraph(
                    productSection.CategoryName.ToUpperInvariant());
                category.Format.Font.Size = 12;
                category.Format.Font.Bold = true;
                category.Format.Font.Color = Color.FromRgb(43, 112, 219);
                category.Format.SpaceBefore = sectionIndex == 0
                    ? Unit.FromMillimeter(1)
                    : Unit.FromMillimeter(5);
                category.Format.SpaceAfter = Unit.FromMillimeter(2);
                category.Format.KeepWithNext = true;
            }

            var table = AddProductTable(
                section,
                priceTable.PriceColumnTitle);
            for (var rowIndex = 0;
                 rowIndex < productSection.Rows.Count;
                 rowIndex++)
            {
                var item = productSection.Rows[rowIndex];
                var row = table.AddRow();
                if (rowIndex % 2 == 1)
                {
                    row.Shading.Color = Color.FromRgb(241, 245, 249);
                }

                row.TopPadding = Unit.FromMillimeter(2);
                row.BottomPadding = Unit.FromMillimeter(2);
                row.Cells[0].VerticalAlignment = VerticalAlignment.Center;
                row.Cells[1].VerticalAlignment = VerticalAlignment.Center;
                row.Cells[2].VerticalAlignment = VerticalAlignment.Center;
                AddPreview(row.Cells[0], imagePaths[imageIndex]);
                SetCellText(row.Cells[1], item.Title, bold: true);
                SetCellText(
                    row.Cells[2],
                    item.Price.ToString(
                        "C",
                        ViewModels.MainWindowViewModel.BrazilianCulture),
                    ParagraphAlignment.Right,
                    bold: true);
                imageIndex++;
            }
        }

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 7.5;
        footer.Format.Font.Color = Color.FromRgb(118, 129, 143);
        footer.AddText($"{priceTable.Title} • Página ");
        footer.AddPageField();
        return document;
    }

    private static Table AddProductTable(
        Section section,
        string priceColumnTitle)
    {
        var table = section.AddTable();
        table.Borders.Width = 0.4;
        table.Borders.Color = Color.FromRgb(205, 213, 223);
        table.Rows.LeftIndent = 0;
        table.AddColumn(Unit.FromCentimeter(2.3));
        table.AddColumn(Unit.FromCentimeter(11.2));
        table.AddColumn(Unit.FromCentimeter(3.5));

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Color.FromRgb(32, 64, 105);
        header.Format.Font.Bold = true;
        header.Format.Font.Color = Colors.White;
        header.TopPadding = Unit.FromMillimeter(2.3);
        header.BottomPadding = Unit.FromMillimeter(2.3);
        SetCellText(header.Cells[0], "FOTO", ParagraphAlignment.Center);
        SetCellText(header.Cells[1], "TÍTULO");
        SetCellText(
            header.Cells[2],
            priceColumnTitle.ToUpperInvariant(),
            ParagraphAlignment.Right);
        return table;
    }

    private static void AddPreview(Cell cell, string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            SetCellText(cell, "—", ParagraphAlignment.Center);
            return;
        }

        try
        {
            var image = cell.AddImage(imagePath);
            image.LockAspectRatio = true;
            image.Width = Unit.FromCentimeter(1.55);
            image.Height = Unit.FromCentimeter(1.55);
        }
        catch
        {
            SetCellText(cell, "—", ParagraphAlignment.Center);
        }
    }

    private static async Task<IReadOnlyList<string?>> PrepareImagesAsync(
        IReadOnlyList<ProductPriceTableRow> rows,
        string temporaryDirectory)
    {
        using var semaphore = new SemaphoreSlim(8);
        var tasks = rows.Select(async (row, index) =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await PrepareImageAsync(
                    row.ImageUrl,
                    Path.Combine(temporaryDirectory, $"{index:00000}.png"));
            }
            finally
            {
                semaphore.Release();
            }
        });
        return await Task.WhenAll(tasks);
    }

    private static async Task<string?> PrepareImageAsync(
        string? imageUrl,
        string outputPath)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        try
        {
            var bytes = await ReadImageAsync(imageUrl);
            if (bytes is null)
            {
                return null;
            }

            using var source = SKBitmap.Decode(bytes);
            if (source is null || source.Width <= 0 || source.Height <= 0)
            {
                return null;
            }

            var scale = Math.Min(120d / source.Width, 120d / source.Height);
            var width = Math.Max(1, (int)Math.Round(source.Width * scale));
            var height = Math.Max(1, (int)Math.Round(source.Height * scale));
            using var thumbnail = source.Resize(
                new SKImageInfo(width, height),
                new SKSamplingOptions(
                    SKFilterMode.Linear,
                    SKMipmapMode.Linear));
            if (thumbnail is null)
            {
                return null;
            }

            using var image = SKImage.FromBitmap(thumbnail);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);
            using var output = File.Create(outputPath);
            encoded.SaveTo(output);
            return outputPath;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]?> ReadImageAsync(string imageUrl)
    {
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
        {
            using var response = await HttpClient.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentLength > MaximumImageBytes)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            return bytes.Length <= MaximumImageBytes ? bytes : null;
        }

        var localPath = uri?.IsFile == true ? uri.LocalPath : imageUrl;
        var info = new FileInfo(localPath);
        return !info.Exists || info.Length > MaximumImageBytes
            ? null
            : await File.ReadAllBytesAsync(localPath);
    }

    private static void SetCellText(
        Cell cell,
        string text,
        ParagraphAlignment alignment = ParagraphAlignment.Left,
        bool bold = false)
    {
        var paragraph = cell.AddParagraph();
        paragraph.Format.Alignment = alignment;
        paragraph.AddFormattedText(
            text,
            bold ? TextFormat.Bold : TextFormat.NotBold);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) BuildPC/1.0");
        return client;
    }
}
