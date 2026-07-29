using BuildPc.Core.Models;

namespace BuildPc.Desktop.Services;

public static class ProductPriceTableSectionFactory
{
    public static IReadOnlyList<ProductPriceTableSection> Create(
        ProductPriceTableDocument priceTable)
    {
        ArgumentNullException.ThrowIfNull(priceTable);
        if (!priceTable.GroupByCategory)
        {
            return [new ProductPriceTableSection(null, priceTable.Rows)];
        }

        return priceTable.Rows
            .GroupBy(row => row.Category)
            .OrderBy(group => ComponentCategoryInfo.DisplayOrder(group.Key))
            .Select(group => new ProductPriceTableSection(
                CategoryName(group),
                group.ToList()))
            .ToList();
    }

    private static string CategoryName(
        IGrouping<ComponentCategory, ProductPriceTableRow> group)
    {
        var configuredName = group
            .Select(row => row.CategoryName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        return string.IsNullOrWhiteSpace(configuredName)
            ? group.Key.ToString()
            : configuredName;
    }
}
