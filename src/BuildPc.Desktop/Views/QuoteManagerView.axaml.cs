using Avalonia.Controls;
using Avalonia.Platform.Storage;
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

    private async void ExportPdf_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not QuoteManagerViewModel { SelectedQuote: { } selected } ||
            TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar orçamento para PDF",
            SuggestedFileName = $"orcamento-{selected.Quote.Number:000000}.pdf",
            DefaultExtension = "pdf",
            FileTypeChoices =
            [
                new FilePickerFileType("Documento PDF") { Patterns = ["*.pdf"] }
            ]
        });
        if (file is not null)
        {
            var outputPath = file.Path.LocalPath;
            new QuotePdfService().Export(selected.Quote, outputPath);
            SystemFileLauncher.Open(outputPath);
        }
    }
}
