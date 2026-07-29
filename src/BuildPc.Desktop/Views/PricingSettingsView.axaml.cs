using Avalonia.Controls;
using Avalonia.Platform.Storage;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Desktop.Views;

public partial class PricingSettingsView : UserControl
{
    public PricingSettingsView() => InitializeComponent();

    private async void BrowseLogo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not PricingSettingsViewModel viewModel ||
            TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar logomarca",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Imagens")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg"]
                }
            ]
        });
        var file = files.FirstOrDefault();
        if (file is not null)
        {
            viewModel.LogoPath = file.Path.LocalPath;
        }
    }
}
