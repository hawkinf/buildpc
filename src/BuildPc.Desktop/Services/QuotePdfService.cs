using BuildPc.Core.Models;
using BuildPc.Desktop.ViewModels;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace BuildPc.Desktop.Services;

public sealed class QuotePdfService
{
    private static readonly object FontLock = new();
    private static bool _fontsConfigured;

    public void Export(SavedQuote quote, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ConfigureFonts();

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = BuildDocument(quote);
        var renderer = new PdfDocumentRenderer
        {
            Document = document
        };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(outputPath);
    }

    private static Document BuildDocument(SavedQuote quote)
    {
        var document = new Document
        {
            Info =
            {
                Title = $"Orçamento #{quote.Number:000000}",
                Author = string.IsNullOrWhiteSpace(quote.CompanySnapshot.CompanyName)
                    ? "BuildPC"
                    : quote.CompanySnapshot.CompanyName,
                Subject = $"Orçamento para {quote.ClientName}"
            }
        };
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 9;
        normal.Font.Color = Color.FromRgb(35, 43, 55);

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.5);

        AddHeader(section, quote);
        AddClient(section, quote);
        AddItems(section, quote);
        AddTotals(section, quote);
        AddNotes(section, quote);
        AddFooter(section, quote);
        return document;
    }

    private static void AddHeader(Section section, SavedQuote quote)
    {
        var company = quote.CompanySnapshot;
        var table = section.AddTable();
        table.Borders.Visible = false;
        table.AddColumn(Unit.FromCentimeter(5));
        table.AddColumn(Unit.FromCentimeter(12));
        var row = table.AddRow();

        if (!string.IsNullOrWhiteSpace(company.LogoPath) &&
            File.Exists(company.LogoPath))
        {
            var logo = row.Cells[0].AddImage(company.LogoPath);
            logo.LockAspectRatio = true;
            logo.Width = Unit.FromCentimeter(4.2);
            logo.Height = Unit.FromCentimeter(2);
        }
        else
        {
            var brand = row.Cells[0].AddParagraph(
                string.IsNullOrWhiteSpace(company.CompanyName)
                    ? "BUILDPC"
                    : company.CompanyName);
            brand.Format.Font.Size = 18;
            brand.Format.Font.Bold = true;
            brand.Format.Font.Color = Color.FromRgb(43, 112, 219);
        }

        var details = row.Cells[1].AddParagraph();
        details.Format.Alignment = ParagraphAlignment.Right;
        details.Format.Font.Size = 8.5;
        AddLine(details, company.CompanyName, true);
        AddLine(details, company.CompanyDocument);
        AddLine(details, PhoneNumberFormatter.FormatBrazilian(company.CompanyPhone));
        AddLine(details, company.CompanyEmail);
        AddLine(details, company.CompanyWebsite);
        AddLine(details, company.CompanyAddress);

        var title = section.AddParagraph();
        title.Format.SpaceBefore = Unit.FromCentimeter(0.5);
        title.Format.SpaceAfter = Unit.FromCentimeter(0.35);
        title.Format.Font.Size = 20;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Color.FromRgb(22, 55, 93);
        title.AddText($"ORÇAMENTO #{quote.Number:000000}");

        var date = section.AddParagraph(
            $"Emitido em {quote.CreatedAt.LocalDateTime:dd/MM/yyyy 'às' HH:mm}");
        date.Format.Font.Size = 9;
        date.Format.Font.Color = Color.FromRgb(91, 103, 117);
        date.Format.SpaceAfter = Unit.FromCentimeter(0.35);
    }

    private static void AddClient(Section section, SavedQuote quote)
    {
        var box = section.AddTable();
        box.Borders.Width = 0.5;
        box.Borders.Color = Color.FromRgb(205, 213, 223);
        box.Shading.Color = Color.FromRgb(245, 248, 252);
        box.AddColumn(Unit.FromCentimeter(10));
        box.AddColumn(Unit.FromCentimeter(7));
        var row = box.AddRow();
        row.TopPadding = Unit.FromMillimeter(3);
        row.BottomPadding = Unit.FromMillimeter(3);
        AddLabelValue(row.Cells[0], "CLIENTE", quote.ClientName);
        AddLabelValue(
            row.Cells[1],
            "TELEFONE",
            PhoneNumberFormatter.FormatBrazilian(quote.ClientPhone));
        section.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.12);
    }

    private static void AddItems(Section section, SavedQuote quote)
    {
        var table = section.AddTable();
        table.Borders.Width = 0.4;
        table.Borders.Color = Color.FromRgb(205, 213, 223);
        table.Rows.LeftIndent = 0;
        table.AddColumn(Unit.FromCentimeter(1.1));
        table.AddColumn(Unit.FromCentimeter(2.5));
        table.AddColumn(Unit.FromCentimeter(7.3));
        table.AddColumn(Unit.FromCentimeter(2.8));
        table.AddColumn(Unit.FromCentimeter(3.3));

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Color.FromRgb(32, 64, 105);
        header.Format.Font.Bold = true;
        header.Format.Font.Color = Colors.White;
        header.TopPadding = Unit.FromMillimeter(2.5);
        header.BottomPadding = Unit.FromMillimeter(2.5);
        SetCellText(header.Cells[0], "QTD", ParagraphAlignment.Center);
        SetCellText(header.Cells[1], "CATEGORIA");
        SetCellText(header.Cells[2], "PRODUTO");
        SetCellText(header.Cells[3], "UNITÁRIO", ParagraphAlignment.Right);
        SetCellText(header.Cells[4], "TOTAL", ParagraphAlignment.Right);

        for (var index = 0; index < quote.Items.Count; index++)
        {
            var item = quote.Items[index];
            var row = table.AddRow();
            if (index % 2 == 1)
            {
                row.Shading.Color = Color.FromRgb(241, 245, 249);
            }

            row.TopPadding = Unit.FromMillimeter(2.5);
            row.BottomPadding = Unit.FromMillimeter(2.5);
            SetCellText(row.Cells[0], item.Quantity.ToString(), ParagraphAlignment.Center);
            SetCellText(row.Cells[1], item.CategoryName);
            var product = row.Cells[2].AddParagraph();
            product.AddFormattedText(item.Name, TextFormat.Bold);
            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                product.AddLineBreak();
                var description = product.AddFormattedText(item.Description);
                description.Font.Size = 7.5;
                description.Font.Color = Color.FromRgb(91, 103, 117);
            }

            SetCellText(row.Cells[3], Money(item.UnitPrice), ParagraphAlignment.Right);
            SetCellText(row.Cells[4], Money(item.TotalPrice), ParagraphAlignment.Right, true);
        }
    }

    private static void AddTotals(Section section, SavedQuote quote)
    {
        var table = section.AddTable();
        table.Borders.Visible = false;
        table.AddColumn(Unit.FromCentimeter(10.5));
        table.AddColumn(Unit.FromCentimeter(6.5));
        var row = table.AddRow();
        row.Cells[1].Shading.Color = Color.FromRgb(232, 240, 252);
        row.TopPadding = Unit.FromMillimeter(4);
        row.BottomPadding = Unit.FromMillimeter(4);
        var total = row.Cells[1].AddParagraph();
        total.Format.Alignment = ParagraphAlignment.Right;
        total.AddFormattedText("TOTAL DO ORÇAMENTO", TextFormat.Bold);
        total.AddLineBreak();
        var value = total.AddFormattedText(Money(quote.TotalPrice), TextFormat.Bold);
        value.Font.Size = 17;
        value.Font.Color = Color.FromRgb(30, 86, 160);
    }

    private static void AddNotes(Section section, SavedQuote quote)
    {
        var text = string.Join(
            Environment.NewLine + Environment.NewLine,
            new[] { quote.Notes, quote.CompanySnapshot.AdditionalQuoteInfo }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var title = section.AddParagraph("INFORMAÇÕES ADICIONAIS");
        title.Format.SpaceBefore = Unit.FromCentimeter(0.45);
        title.Format.SpaceAfter = Unit.FromCentimeter(0.15);
        title.Format.Font.Bold = true;
        title.Format.Font.Size = 9;
        title.Format.Font.Color = Color.FromRgb(32, 64, 105);
        var paragraph = section.AddParagraph(text);
        paragraph.Format.Font.Size = 8.5;
        paragraph.Format.LineSpacing = 12;
    }

    private static void AddFooter(Section section, SavedQuote quote)
    {
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 7.5;
        footer.Format.Font.Color = Color.FromRgb(118, 129, 143);
        footer.AddText($"Orçamento #{quote.Number:000000} • ");
        footer.AddText("Página ");
        footer.AddPageField();
    }

    private static void AddLine(Paragraph paragraph, string value, bool bold = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (paragraph.Elements.Count > 0)
        {
            paragraph.AddLineBreak();
        }

        paragraph.AddFormattedText(value, bold ? TextFormat.Bold : TextFormat.NotBold);
    }

    private static void AddLabelValue(Cell cell, string label, string value)
    {
        var paragraph = cell.AddParagraph();
        var labelText = paragraph.AddFormattedText(label);
        labelText.Font.Size = 7;
        labelText.Font.Bold = true;
        labelText.Font.Color = Color.FromRgb(91, 103, 117);
        paragraph.AddLineBreak();
        var valueText = paragraph.AddFormattedText(value, TextFormat.Bold);
        valueText.Font.Size = 10;
    }

    private static void SetCellText(
        Cell cell,
        string text,
        ParagraphAlignment alignment = ParagraphAlignment.Left,
        bool bold = false)
    {
        var paragraph = cell.AddParagraph();
        paragraph.Format.Alignment = alignment;
        paragraph.AddFormattedText(text, bold ? TextFormat.Bold : TextFormat.NotBold);
    }

    private static string Money(decimal value) =>
        value.ToString("C", MainWindowViewModel.BrazilianCulture);

    private static void ConfigureFonts()
    {
        lock (FontLock)
        {
            if (_fontsConfigured)
            {
                return;
            }

            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            _fontsConfigured = true;
        }
    }
}
