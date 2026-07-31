using BuildPc.Core.Models;
using BuildPc.Core.Services;

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
                CompanySnapshot = new CompanySnapshot
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
    public void Export_WithoutDescriptions_StillCreatesReadablePdf()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-orcamento-sem-descricao-{Guid.NewGuid():N}.pdf");
        try
        {
            var quote = new SavedQuote
            {
                Id = Guid.NewGuid(),
                Number = 43,
                CreatedAt = new DateTimeOffset(2026, 7, 29, 14, 35, 0, TimeSpan.FromHours(-3)),
                ClientName = "Mariana Oliveira",
                ClientPhone = "(11) 98765-4321",
                TotalCost = 2400m,
                TotalPrice = 3000m,
                CompanySnapshot = new CompanySnapshot { CompanyName = "BuildPC Tecnologia" },
                Items = [Item("Processador", "AMD Ryzen 7 7800X3D", 1, 3000m)]
            };

            new QuotePdfService().Export(quote, path, includeDescriptions: false);

            Assert.True(File.Exists(path));
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 1000);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }
        finally
        {
            if (File.Exists(path))
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

    [Fact]
    public async Task ProductCostTableExport_CreatesCategorySections()
    {
        var requestedPath = Environment.GetEnvironmentVariable(
            "BUILDPC_COST_TABLE_PDF_SAMPLE_PATH");
        var shouldKeep = !string.IsNullOrWhiteSpace(requestedPath);
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-cost-table-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var pdfPath = requestedPath ??
                      Path.Combine(directory, "tabela-custo-categorias.pdf");
        try
        {
            var categories = new[]
            {
                (ComponentCategory.Processor, "Processadores", "Ryzen"),
                (ComponentCategory.GraphicsCard, "Placas de vídeo", "GeForce"),
                (ComponentCategory.Storage, "SSD / NVMe", "NVMe")
            };
            var rows = categories
                .SelectMany(category => Enumerable.Range(1, 12)
                    .Select(index => new ProductPriceTableRow(
                        $"{category.Item3} modelo {index:00} - produto para validação visual",
                        null,
                        149.90m + index * 100m,
                        category.Item1,
                        category.Item2)))
                .ToList();
            var table = new ProductPriceTableDocument(
                "Tabela de custo",
                "Custo",
                "Todos",
                string.Empty,
                "BuildPC Tecnologia",
                new DateTimeOffset(
                    2026,
                    7,
                    29,
                    15,
                    0,
                    0,
                    TimeSpan.FromHours(-3)),
                rows,
                GroupByCategory: true);

            await new ProductPriceTablePdfService().ExportAsync(table, pdfPath);

            Assert.True(File.Exists(pdfPath));
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

    [Fact]
    public void Export_WithDiscountAndTermsProducesAValidPdf()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-desconto-{Guid.NewGuid():N}.pdf");
        try
        {
            var quote = new SavedQuote
            {
                Id = Guid.NewGuid(),
                Number = 77,
                CreatedAt = DateTimeOffset.Now,
                ClientName = "Cliente Com Desconto",
                ClientPhone = "(11) 90000-1111",
                TotalCost = 800m,
                TotalPrice = 1000m,
                DiscountAmount = 100m,
                ValidityDays = 10,
                PaymentTerms = "3x sem juros",
                DeliveryTerms = "5 dias úteis",
                CompanySnapshot = new CompanySnapshot { CompanyName = "BuildPC" },
                Items = [Item("Processador", "CPU Teste", 1, 1000m)]
            };

            new QuotePdfService().Export(quote, path);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
            Assert.Equal(900m, quote.FinalPrice);
            Assert.Equal(quote.CreatedAt.AddDays(10), quote.ValidUntil);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FinalPriceNeverGoesBelowZeroAndValidUntilIsOptional()
    {
        var quote = new SavedQuote
        {
            TotalPrice = 100m,
            DiscountAmount = 250m
        };

        // O modelo protege o total mesmo se um desconto maior for gravado por
        // uma versão anterior ou por outra origem de dados.
        Assert.Equal(0m, quote.FinalPrice);
        Assert.Null(quote.ValidUntil);

        var withoutDiscount = new SavedQuote { TotalPrice = 100m };
        Assert.Equal(100m, withoutDiscount.FinalPrice);
        Assert.False(withoutDiscount.HasDiscount);
    }

    /// <summary>
    /// Guarda estrutural: o PDF do cliente nunca pode mostrar custo ou
    /// lucro (regra de negócio central do orçamento). Os testes acima só
    /// confirmam que o PDF gerado é válido, não o que está escrito nele —
    /// PdfSharp não expõe extração de texto, então a rede de proteção aqui é
    /// no nível do código-fonte: se algum dia um campo de custo/lucro for
    /// referenciado dentro de QuotePdfService, este teste falha antes que o
    /// vazamento chegue a um PDF real.
    /// </summary>
    [Fact]
    public void SourceNeverReferencesCostOrProfitFields()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BuildPc.Core",
            "Services",
            "QuotePdfService.cs"));

        Assert.DoesNotContain("UnitCost", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TotalCost", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Profit", source, StringComparison.Ordinal);
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
