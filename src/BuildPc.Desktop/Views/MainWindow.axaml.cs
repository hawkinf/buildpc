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
