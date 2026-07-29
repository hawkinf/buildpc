using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace BuildPc.Desktop.Controls;

public sealed partial class ProductHoverPreview : UserControl
{
    public static readonly StyledProperty<Control?> PlacementTargetProperty =
        AvaloniaProperty.Register<ProductHoverPreview, Control?>(
            nameof(PlacementTarget));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ProductHoverPreview, string>(
            nameof(Title),
            string.Empty);

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<ProductHoverPreview, string>(
            nameof(Description),
            string.Empty);

    public static readonly StyledProperty<string?> ImageUrlProperty =
        AvaloniaProperty.Register<ProductHoverPreview, string?>(
            nameof(ImageUrl));

    public static readonly StyledProperty<string> PriceProperty =
        AvaloniaProperty.Register<ProductHoverPreview, string>(
            nameof(Price),
            string.Empty);

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ProductHoverPreview, bool>(
            nameof(IsExpanded));

    private static WeakReference<ProductHoverPreview>? _activePreview;
    private Control? _hookedTarget;
    private int _hoverVersion;

    public ProductHoverPreview()
    {
        InitializeComponent();
    }

    public Control? PlacementTarget
    {
        get => GetValue(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string? ImageUrl
    {
        get => GetValue(ImageUrlProperty);
        set => SetValue(ImageUrlProperty, value);
    }

    public string Price
    {
        get => GetValue(PriceProperty);
        set => SetValue(PriceProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        private set => SetValue(IsExpandedProperty, value);
    }

    protected override void OnPropertyChanged(
        AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlacementTargetProperty)
        {
            AttachTo(change.NewValue as Control);
        }
    }

    protected override void OnDetachedFromVisualTree(
        VisualTreeAttachmentEventArgs e)
    {
        CloseImmediately();
        AttachTo(null);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnAttachedToVisualTree(
        VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachTo(PlacementTarget);
    }

    private void AttachTo(Control? target)
    {
        if (ReferenceEquals(_hookedTarget, target))
        {
            return;
        }

        if (_hookedTarget is not null)
        {
            _hookedTarget.PointerEntered -= Target_PointerEntered;
            _hookedTarget.PointerExited -= Target_PointerExited;
        }

        _hookedTarget = target;
        if (_hookedTarget is not null)
        {
            _hookedTarget.PointerEntered += Target_PointerEntered;
            _hookedTarget.PointerExited += Target_PointerExited;
        }
    }

    private async void Target_PointerEntered(
        object? sender,
        PointerEventArgs e)
    {
        var target = _hookedTarget;
        if (target is null)
        {
            return;
        }

        var version = ++_hoverVersion;
        await Task.Delay(220);
        if (version != _hoverVersion ||
            !ReferenceEquals(target, _hookedTarget) ||
            !target.IsPointerOver)
        {
            return;
        }

        if (_activePreview?.TryGetTarget(out var active) == true &&
            !ReferenceEquals(active, this))
        {
            active.CloseImmediately();
        }

        _activePreview = new(this);
        IsExpanded = false;
        previewPopup.IsOpen = true;

        await Task.Delay(16);
        if (version == _hoverVersion &&
            ReferenceEquals(target, _hookedTarget) &&
            target.IsPointerOver &&
            previewPopup.IsOpen)
        {
            IsExpanded = true;
        }
    }

    private async void Target_PointerExited(
        object? sender,
        PointerEventArgs e)
    {
        var version = ++_hoverVersion;
        IsExpanded = false;
        await Task.Delay(170);
        if (version == _hoverVersion)
        {
            CloseImmediately();
        }
    }

    private void CloseImmediately()
    {
        _hoverVersion++;
        IsExpanded = false;
        previewPopup.IsOpen = false;

        if (_activePreview?.TryGetTarget(out var active) == true &&
            ReferenceEquals(active, this))
        {
            _activePreview = null;
        }
    }
}
