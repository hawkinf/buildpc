using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BuildPc.Desktop.Services;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Desktop.Views;

public partial class FlexibleListView : UserControl
{
    public FlexibleListView()
    {
        InitializeComponent();
        var eyeButton = this.FindControl<Button>("sensitiveTotalsButton");
        if (eyeButton is not null)
        {
            eyeButton.AddHandler(
                PointerPressedEvent,
                SensitiveTotals_PointerPressed,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            eyeButton.AddHandler(
                PointerReleasedEvent,
                SensitiveTotals_PointerReleased,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            eyeButton.PointerCaptureLost += (_, _) => HideSensitiveTotals();
        }
    }

    private void SensitiveTotals_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Button button)
        {
            e.Pointer.Capture(button);
        }

        if (DataContext is FlexibleListViewModel viewModel)
        {
            viewModel.SetSensitiveTotalsVisible(true);
        }
    }

    private void SensitiveTotals_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        HideSensitiveTotals();
    }

    private void HideSensitiveTotals()
    {
        if (DataContext is FlexibleListViewModel viewModel)
        {
            viewModel.SetSensitiveTotalsVisible(false);
        }
    }

    private void ExportPdf_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not FlexibleListViewModel
            {
                CanExport: true,
                SavedQuote: { } quote
            } viewModel)
        {
            return;
        }

        var outputPath = PdfPreviewService.CreatePath(
            $"orcamento-{quote.Number:000000}.pdf");
        try
        {
            new QuotePdfService().Export(quote, outputPath);
            viewModel.CompletePdfPreview(SystemFileLauncher.Open(outputPath));
        }
        catch
        {
            viewModel.FailPdfPreview();
        }
    }
}
