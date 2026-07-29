using BuildPc.Core.Models;
using BuildPc.Desktop.Services;

namespace BuildPc.Core.Tests;

public sealed class QuotePdfServiceTests
{
    [Fact]
    public void Export_CreatesReadablePdf()
    {
        var requestedPath = Environment.GetEnvironmentVariable("BUILDPC_PDF_SAMPLE_PATH");
        var shouldKeep = !string.IsNullOrWhiteSpace(requestedPath);
        var path = requestedPath ?? Path.Combine(
            Path.GetTempPath(),
            $"buildpc-orcamento-{Guid.NewGuid():N}.pdf");
        try
        {
            var quote = new SavedQuote
            {
                Id = Guid.NewGuid(),
                Number = 42,
                CreatedAt = new DateTimeOffset(2026, 7, 29, 14, 35, 0, TimeSpan.FromHours(-3)),
                ClientName = "Mariana Oliveira",
                ClientPhone = "(11) 98765-4321",
                Notes = "Montagem e testes inclusos.",
                TotalCost = 3500m,
                TotalPrice = 4375m,
                CompanySnapshot = new BusinessSettings
                {
                    CompanyName = "BuildPC Tecnologia",
                    CompanyDocument = "12.345.678/0001-90",
                    CompanyPhone = "(11) 3333-4455",
                    CompanyEmail = "contato@buildpc.local",
                    CompanyAddress = "Av. Tecnologia, 100 - São Paulo/SP",
                    AdditionalQuoteInfo =
                        "Validade: 7 dias. Pagamento conforme condições comerciais."
                },
                Items =
                [
                    Item("Processador", "AMD Ryzen 7 7800X3D", 1, 2400m),
                    Item("Placa-mãe", "Placa-mãe B650M DDR5", 1, 1100m),
                    Item("Memória", "Memória DDR5 32 GB 6000 MHz", 2, 437.5m)
                ]
            };

            new QuotePdfService().Export(quote, path);

            Assert.True(File.Exists(path));
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 1000);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }
        finally
        {
            if (!shouldKeep && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ProductPriceTableExport_CreatesPdfWithFilteredRows()
    {
        var requestedPath = Environment.GetEnvironmentVariable(
            "BUILDPC_PRICE_TABLE_PDF_SAMPLE_PATH");
        var shouldKeep = !string.IsNullOrWhiteSpace(requestedPath);
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-price-table-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var imagePath = Path.Combine(directory, "preview.png");
        var pdfPath = requestedPath ??
                      Path.Combine(directory, "tabela-venda.pdf");
        try
        {
            await File.WriteAllBytesAsync(
                imagePath,
                Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwC" +
                    "AAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
            var rows = Enumerable.Range(1, 36)
                .Select(index => new ProductPriceTableRow(
                    $"SSD NVMe ADATA {index * 128} GB - modelo para teste de paginação",
                    index == 1 ? imagePath : null,
                    299.90m + index * 25m))
                .ToList();
            var table = new ProductPriceTableDocument(
                "Tabela de venda",
                "Venda",
                "SSD / NVMe",
                "nvme 256 adata",
                "BuildPC Tecnologia",
                new DateTimeOffset(2026, 7, 29, 15, 0, 0, TimeSpan.FromHours(-3)),
                rows);

            await new ProductPriceTablePdfService().ExportAsync(table, pdfPath);

            Assert.True(File.Exists(pdfPath));
            var bytes = await File.ReadAllBytesAsync(pdfPath);
            Assert.True(bytes.Length > 1000);
            Assert.Equal(
                "%PDF",
                System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            using var pdf = PdfSharp.Pdf.IO.PdfReader.Open(
                pdfPath,
                PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
            Assert.True(pdf.PageCount > 1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            if (!shouldKeep && File.Exists(pdfPath))
            {
                File.Delete(pdfPath);
            }
        }
    }

    private static SavedQuoteItem Item(
        string category,
        string name,
        int quantity,
        decimal unitPrice) =>
        new()
        {
            ComponentId = Guid.NewGuid().ToString("N"),
            CategoryName = category,
            Name = name,
            Description = "Produto selecionado conforme especificação do cliente.",
            Quantity = quantity,
            UnitCost = unitPrice * 0.8m,
            MarginPercent = 25m,
            UnitPrice = unitPrice
        };
}
