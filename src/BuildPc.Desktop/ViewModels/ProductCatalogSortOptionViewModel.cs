namespace BuildPc.Desktop.ViewModels;

public enum ProductCatalogSortMode
{
    DescriptionAscending,
    DescriptionDescending,
    PriceAscending,
    PriceDescending
}

public sealed record ProductCatalogSortOptionViewModel(
    string Name,
    ProductCatalogSortMode Mode);
