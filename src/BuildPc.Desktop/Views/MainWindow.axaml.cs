using System.Text;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using BuildPc.Desktop.Services;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherTimer _connectionStatusTimer;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _connectionStatusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _connectionStatusTimer.Tick += RefreshConnectionStatus;
        Opened += WindowOpened;
        Closed += WindowClosed;
    }

    private async void WindowOpened(object? sender, EventArgs e)
    {
        _connectionStatusTimer.Start();
        try
        {
            await RefreshConnectionStatusAsync();
        }
        catch (Exception exception)
        {
            CrashLogService.Record("Estado da conexão", exception);
        }
    }

    private void WindowClosed(object? sender, EventArgs e) =>
        _connectionStatusTimer.Stop();

    private async void RefreshConnectionStatus(object? sender, EventArgs e)
    {
        try
        {
            await RefreshConnectionStatusAsync();
        }
        catch (Exception exception)
        {
            CrashLogService.Record("Estado da conexão", exception);
        }
    }

    private async Task RefreshConnectionStatusAsync()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.ConnectionStatus.RefreshAsync();
        }
    }

    private async void SelectProductImage_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
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
        catch (Exception exception)
        {
            CrashLogService.Record("Seleção de foto", exception);
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

    private async void ExportCatalogCsv_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Exportar catálogo para CSV",
                    SuggestedFileName = viewModel.CatalogCsvSuggestedFileName,
                    DefaultExtension = "csv",
                    FileTypeChoices = [CsvFileType]
                });
            if (file is null)
            {
                return;
            }

            // BOM UTF-8: sem ele o Excel em português corrompe os acentos.
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            await writer.WriteAsync(viewModel.BuildCatalogCsv());
        }
        catch (Exception exception)
        {
            CrashLogService.Record("Exportar CSV", exception);
            viewModel.ReportCatalogCsvFailure();
        }
    }

    private async void ImportCatalogCsv_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Importar produtos de um CSV",
                    AllowMultiple = false,
                    FileTypeFilter = [CsvFileType]
                });
            if (files.FirstOrDefault() is not { } file)
            {
                return;
            }

            string csv;
            await using (var stream = await file.OpenReadAsync())
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                csv = await reader.ReadToEndAsync();
            }

            await viewModel.ImportCatalogCsvAsync(csv);
        }
        catch (Exception exception)
        {
            CrashLogService.Record("Importar CSV", exception);
            viewModel.ReportCatalogCsvFailure();
        }
    }

    private static FilePickerFileType CsvFileType { get; } =
        new("Planilha CSV")
        {
            Patterns = ["*.csv"],
            MimeTypes = ["text/csv"]
        };

    private async void CatalogProduct_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Control
            {
                DataContext: ProductListItemViewModel product
            } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        product.SelectCommand.Execute(null);
        viewModel.EditProductCommand.Execute(null);
        if (!viewModel.IsEditingProduct)
        {
            return;
        }

        e.Handled = true;
        try
        {
            var editor = new ProductEditWindow
            {
                DataContext = viewModel
            };
            await editor.ShowDialog(this);
        }
        catch (Exception exception)
        {
            CrashLogService.Record("Edição de produto", exception);
        }
    }
}
