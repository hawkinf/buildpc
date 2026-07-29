using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        var eyeButton = this.FindControl<Button>("productCostEyeButton");
        if (eyeButton is not null)
        {
            eyeButton.AddHandler(
                PointerPressedEvent,
                ProductCostEye_PointerPressed,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            eyeButton.AddHandler(
                PointerReleasedEvent,
                ProductCostEye_PointerReleased,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            eyeButton.PointerCaptureLost += (_, _) => HideProductCost();
        }
    }

    private void ProductCostEye_PointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        if (sender is Button button)
        {
            e.Pointer.Capture(button);
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetProductEditCostVisible(true);
        }
    }

    private void ProductCostEye_PointerReleased(
        object? sender,
        PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        HideProductCost();
    }

    private void HideProductCost()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetProductEditCostVisible(false);
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

        HideProductCost();
        _completed = true;
        Close();
    }

    private void Cancel_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        HideProductCost();
        CancelEditing();
        Close();
    }

    private void WindowClosing(
        object? sender,
        WindowClosingEventArgs e)
    {
        HideProductCost();
        CancelEditing();
    }

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
