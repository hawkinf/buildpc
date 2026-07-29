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

        // Tunelamento: os atalhos precisam funcionar mesmo com o foco dentro de
        // um campo de texto da montagem.
        AddHandler(
            KeyDownEvent,
            Root_KeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
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

    /// <summary>
    /// Atalhos da Montagem: Ctrl+S grava, Ctrl+P exporta, Ctrl+L limpa e Esc
    /// fecha a confirmação aberta.
    /// </summary>
    private void Root_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FlexibleListViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Escape && viewModel.IsClearConfirmationVisible)
        {
            viewModel.CancelClearCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        switch (e.Key)
        {
            case Key.S:
                viewModel.SaveQuoteCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.P:
                ExportPdf_Click(sender, e);
                e.Handled = true;
                break;
            case Key.L:
                viewModel.RequestClearCommand.Execute(null);
                e.Handled = true;
                break;
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
