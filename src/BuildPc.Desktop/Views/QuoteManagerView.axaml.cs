using Avalonia.Controls;
using BuildPc.Core.Services;
using BuildPc.Desktop.Services;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Desktop.Views;

public partial class QuoteManagerView : UserControl
{
    public QuoteManagerView() => InitializeComponent();

    private async void Refresh_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not QuoteManagerViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.RefreshAsync();
        }
        catch (Exception exception)
        {
            CrashLogService.Record("Atualizar orçamentos", exception);
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
            new QuotePdfService().Export(
                selected.Quote,
                outputPath,
                viewModel.IncludeDescriptionsInPdf);
            viewModel.CompletePdfPreview(SystemFileLauncher.Open(outputPath));
        }
        catch
        {
            viewModel.FailPdfPreview();
        }
    }
}
