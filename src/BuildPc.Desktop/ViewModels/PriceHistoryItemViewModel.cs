using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

/// <summary>
/// Uma linha do histórico de custo de um produto, já com a variação em relação
/// ao preço seguinte (mais recente) calculada.
/// </summary>
public sealed class PriceHistoryItemViewModel
{
    public PriceHistoryItemViewModel(
        PriceHistoryEntry entry,
        decimal priceAfterThisChange)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Price = entry.Price.ToString("C", MainWindowViewModel.BrazilianCulture);
        RecordedAt = entry.RecordedAt.LocalDateTime.ToString(
            "dd/MM/yyyy HH:mm",
            MainWindowViewModel.BrazilianCulture);
        Source = string.IsNullOrWhiteSpace(entry.Source) ? "manual" : entry.Source;

        var difference = priceAfterThisChange - entry.Price;
        IsIncrease = difference > 0;
        HasChange = difference != 0;
        Difference = difference == 0
            ? string.Empty
            : $"{(difference > 0 ? "+" : "-")}" +
              Math.Abs(difference).ToString("C", MainWindowViewModel.BrazilianCulture);
        PercentChange = entry.Price <= 0 || difference == 0
            ? string.Empty
            : $"{(difference > 0 ? "+" : "")}" +
              decimal.Round(
                      difference / entry.Price * 100m,
                      2,
                      MidpointRounding.AwayFromZero)
                  .ToString("N2", MainWindowViewModel.BrazilianCulture) + "%";
    }

    public string Price { get; }
    public string RecordedAt { get; }
    public string Source { get; }
    public string Difference { get; }
    public string PercentChange { get; }
    public bool IsIncrease { get; }
    public bool HasChange { get; }
    public bool IsDecrease => HasChange && !IsIncrease;
}
