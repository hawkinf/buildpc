using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Desktop.ViewModels;

public sealed class SavedQuoteListItemViewModel(SavedQuote quote) : ViewModelBase
{
    public SavedQuote Quote { get; } = quote;
    public IReadOnlyList<SavedQuoteItemListItemViewModel> Items { get; } =
        quote.Items
            .Select((item, index) =>
                new SavedQuoteItemListItemViewModel(item, index % 2 == 1))
            .ToList();
    public string NumberText => $"Orçamento #{Quote.Number:000000}";
    public string DateText => Quote.CreatedAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm");
    public string ClientText => Quote.ClientName;
    public string PhoneText => PhoneNumberFormatter.FormatBrazilian(Quote.ClientPhone);
    public string ItemsText => Quote.Items.Count == 1 ? "1 item" : $"{Quote.Items.Count} itens";
    public string TotalText => Quote.TotalPrice.ToString("C", MainWindowViewModel.BrazilianCulture);
    public string CostText =>
        Quote.TotalCost.ToString("C", MainWindowViewModel.BrazilianCulture);
    public string ProfitText =>
        Quote.TotalProfit.ToString("C", MainWindowViewModel.BrazilianCulture);
    public decimal ProfitPercent => Quote.TotalCost <= 0
        ? 0
        : decimal.Round(
            Quote.TotalProfit / Quote.TotalCost * 100m,
            2,
            MidpointRounding.AwayFromZero);
    public string ProfitPercentText =>
        $"{ProfitPercent.ToString("N2", MainWindowViewModel.BrazilianCulture)}%";
}
