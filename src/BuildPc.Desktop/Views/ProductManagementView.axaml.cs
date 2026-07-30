using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using BuildPc.Desktop.Services;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Desktop.Views;

public sealed partial class ProductManagementView : UserControl
{
    public ProductManagementView()
    {
        InitializeComponent();
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
            eyeButton.AddHandler(KeyDownEvent, ProductCostEye_KeyDown);
            eyeButton.AddHandler(KeyUpEvent, ProductCostEye_KeyUp);
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

    private void ProductCostEye_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Space or Key.Enter))
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetProductEditCostVisible(true);
        }

        e.Handled = true;
    }

    private void ProductCostEye_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Space or Key.Enter))
        {
            return;
        }

        HideProductCost();
        e.Handled = true;
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
