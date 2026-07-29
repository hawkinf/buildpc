using Avalonia.Controls;
using Avalonia.Input;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Desktop.Views;

public sealed partial class PriceLookupView : UserControl
{
    private PriceLookupItemViewModel? _activePreview;

    public PriceLookupView()
    {
        InitializeComponent();
    }

    private async void ProductRow_PointerEntered(
        object? sender,
        PointerEventArgs e)
    {
        if (sender is not Control
            {
                DataContext: PriceLookupItemViewModel item
            } control)
        {
            return;
        }

        await Task.Delay(220);
        if (!control.IsPointerOver)
        {
            return;
        }

        if (_activePreview is not null &&
            !ReferenceEquals(_activePreview, item))
        {
            _activePreview.IsPreviewVisible = false;
            _activePreview.IsPreviewOpen = false;
        }

        _activePreview = item;
        item.IsPreviewOpen = true;
        await Task.Delay(16);
        if (control.IsPointerOver && item.IsPreviewOpen)
        {
            item.IsPreviewVisible = true;
        }
    }

    private async void ProductRow_PointerExited(
        object? sender,
        PointerEventArgs e)
    {
        if (sender is not Control
            {
                DataContext: PriceLookupItemViewModel item
            })
        {
            return;
        }

        item.IsPreviewVisible = false;
        await Task.Delay(170);
        if (!item.IsPreviewVisible)
        {
            item.IsPreviewOpen = false;
            if (ReferenceEquals(_activePreview, item))
            {
                _activePreview = null;
            }
        }
    }
}
