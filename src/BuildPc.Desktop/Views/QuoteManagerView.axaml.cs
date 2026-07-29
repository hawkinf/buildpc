using Avalonia.Controls;
using BuildPc.Desktop.Services;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Desktop.Views;

public partial class QuoteManagerView : UserControl
{
    public QuoteManagerView() => InitializeComponent();

    private void Refresh_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is QuoteManagerViewModel viewModel)
        {
            viewModel.Refresh();
        }
    }

    private void ExportPdf_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not QuoteManagerViewModel
            {
                SelectedQuote: { } selected
            } viewModel)
        {
            return;
        }

        var outputPath = PdfPreviewService.CreatePath(
            $"orcamento-{selected.Quote.Number:000000}.pdf");
        try
        {
            new QuotePdfService().Export(selected.Quote, outputPath);
            viewModel.CompletePdfPreview(SystemFileLauncher.Open(outputPath));
        }
        catch
        {
            viewModel.FailPdfPreview();
        }
    }
}
