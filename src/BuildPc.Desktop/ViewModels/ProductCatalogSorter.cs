namespace BuildPc.Desktop.ViewModels;

public static class ProductCatalogSorter
{
    public static IReadOnlyList<ProductListItemViewModel> Sort(
        IEnumerable<ProductListItemViewModel> products,
        ProductCatalogSortMode mode,
        Func<ProductListItemViewModel, decimal>? priceSelector = null)
    {
        priceSelector ??= product => product.Component.Price;
        return mode switch
        {
            ProductCatalogSortMode.DescriptionDescending => products
                .OrderByDescending(
                    product => product.Description,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(
                    product => product.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            ProductCatalogSortMode.PriceAscending => products
                .OrderBy(priceSelector)
                .ThenBy(
                    product => product.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            ProductCatalogSortMode.PriceDescending => products
                .OrderByDescending(priceSelector)
                .ThenBy(
                    product => product.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            _ => products
                .OrderBy(
                    product => product.Description,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(
                    product => product.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList()
        };
    }
}
