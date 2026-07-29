namespace BuildPc.Desktop.ViewModels;

public enum ProductPriceTableKind
{
    Cost,
    Sale
}

public sealed record ProductPriceTableOptionViewModel(
    ProductPriceTableKind Kind,
    string Name);
