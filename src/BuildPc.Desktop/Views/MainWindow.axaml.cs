using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using BuildPc.Desktop.Services;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Desktop.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);

    private async void SelectProductImage_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar foto do produto",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Imagens")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"]
                }
            ]
        });
        var selected = files.FirstOrDefault();
        if (selected is not null)
        {
            viewModel.SetProductImage(selected.Path.LocalPath);
        }
    }

    private async void ExportProductPriceTablePdf_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel
            {
                CanExportProductPriceTable: true
            } viewModel)
        {
            return;
        }

        var document = viewModel.BuildProductPriceTableDocument();
        viewModel.BeginProductPriceTableExport();
        try
        {
            var outputPath = PdfPreviewService.CreatePath(
                viewModel.ProductPriceTableSuggestedFileName);
            await new ProductPriceTablePdfService().ExportAsync(document, outputPath);
            viewModel.CompleteProductPriceTableExport(
                SystemFileLauncher.Open(outputPath));
        }
        catch
        {
            viewModel.FailProductPriceTableExport();
        }
    }
}
