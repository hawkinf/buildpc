using Avalonia.Controls;
using Avalonia.Platform.Storage;
using BuildPc.Desktop.Services;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Desktop.Views;

public sealed partial class ProductManagementView : UserControl
{
    public ProductManagementView()
    {
        InitializeComponent();
    }

    private async void SelectProductImage_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            return;
        }

        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(
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
}
