using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Desktop.Views;

public sealed partial class ProductEditWindow : Window
{
    private bool _completed;

    public ProductEditWindow()
    {
        AvaloniaXamlLoader.Load(this);
        Closing += WindowClosing;
    }

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

    private void Save_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.SaveProductCommand.Execute(null);
        if (!viewModel.IsProductFormSuccess)
        {
            return;
        }

        _completed = true;
        Close();
    }

    private void Cancel_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        CancelEditing();
        Close();
    }

    private void WindowClosing(
        object? sender,
        WindowClosingEventArgs e) =>
        CancelEditing();

    private void CancelEditing()
    {
        if (_completed ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        _completed = true;
        viewModel.CancelProductEditCommand.Execute(null);
    }
}
