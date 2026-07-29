namespace BuildPc.Desktop.ViewModels;

public static class ProductCatalogSorter
{
    public static IReadOnlyList<ProductListItemViewModel> Sort(
        IEnumerable<ProductListItemViewModel> products,
        ProductCatalogSortMode mode) =>
        mode switch
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
                .OrderBy(product => product.Component.Price)
                .ThenBy(
                    product => product.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            ProductCatalogSortMode.PriceDescending => products
                .OrderByDescending(product => product.Component.Price)
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
