using BuildPc.Core.Models;
using BuildPc.Core.Services;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Desktop.Services;

public static class ProductPriceTableRowFactory
{
    public static IReadOnlyList<ProductPriceTableRow> Create(
        IEnumerable<ProductListItemViewModel> orderedProducts,
        Func<PcComponent, decimal> priceSelector) =>
        orderedProducts
            .Select(product => new ProductPriceTableRow(
                product.Name,
                product.ImageUrl,
                priceSelector(product.Component),
                product.Component.Category,
                product.Category))
            .ToList();
}
