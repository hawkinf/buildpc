using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed class SavedQuoteItemListItemViewModel(
    SavedQuoteItem item,
    bool isAlternate)
{
    public SavedQuoteItem Item { get; } = item;
    public bool IsAlternate { get; } = isAlternate;
    public int Quantity => Item.Quantity;
    public string Name => Item.Name;
    public string Description => Item.Description;
    public string? ImageUrl => Item.ImageUrl;
    public string CategoryName => Item.CategoryName;
    public decimal TotalPrice => Item.TotalPrice;
    public decimal TotalCost => Item.UnitCost * Item.Quantity;
    public string TotalPriceText =>
        TotalPrice.ToString("C", MainWindowViewModel.BrazilianCulture);
    public string TotalCostText =>
        TotalCost.ToString("C", MainWindowViewModel.BrazilianCulture);
}
