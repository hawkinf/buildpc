using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed class SavedQuoteListItemViewModel(SavedQuote quote) : ViewModelBase
{
    public SavedQuote Quote { get; } = quote;
    public string NumberText => $"Orçamento #{Quote.Number:000000}";
    public string DateText => Quote.CreatedAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm");
    public string ClientText => Quote.ClientName;
    public string PhoneText => Quote.ClientPhone;
    public string ItemsText => Quote.Items.Count == 1 ? "1 item" : $"{Quote.Items.Count} itens";
    public string TotalText => Quote.TotalPrice.ToString("C", MainWindowViewModel.BrazilianCulture);
}
