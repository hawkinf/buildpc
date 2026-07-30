using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

public enum PriceTableSortMode
{
    NameAscending,
    NameDescending,
    PriceAscending,
    PriceDescending
}

/// <summary>
/// Filtra, aplica o preço de custo/venda e ordena o catálogo para a tela de
/// Consulta de Preços do cliente web — usado tanto pela página quanto pelo
/// endpoint de PDF, para os dois sempre mostrarem exatamente a mesma tabela.
/// </summary>
public static class PriceTableRowBuilder
{
    public static IReadOnlyList<ProductPriceTableRow> Build(
        IEnumerable<PcComponent> catalog,
        BusinessSettings settings,
        ComponentCategory? categoryFilter,
        string? searchText,
        PriceTableSortMode sortMode,
        bool showSalePrice)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);

        var categoryNames = settings.EffectiveProductCategories()
            .ToDictionary(category => category.Value, category => category.Name);

        var rows = catalog
            .Where(component =>
                (categoryFilter is null || categoryFilter == component.Category) &&
                ProductFilter.Matches(component, searchText))
            .Select(component => new ProductPriceTableRow(
                component.Name,
                component.ImageUrl,
                showSalePrice
                    ? PricingCalculator.CalculateSalePrice(
                        component.Price,
                        settings.MarginFor(component.Category))
                    : component.Price,
                component.Category,
                categoryNames.GetValueOrDefault(
                    component.Category,
                    component.Category.ToString())));

        IEnumerable<ProductPriceTableRow> ordered = sortMode switch
        {
            PriceTableSortMode.NameDescending => rows.OrderByDescending(
                row => row.Title,
                StringComparer.CurrentCultureIgnoreCase),
            PriceTableSortMode.PriceAscending => rows
                .OrderBy(row => row.Price)
                .ThenBy(row => row.Title, StringComparer.CurrentCultureIgnoreCase),
            PriceTableSortMode.PriceDescending => rows
                .OrderByDescending(row => row.Price)
                .ThenBy(row => row.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => rows.OrderBy(row => row.Title, StringComparer.CurrentCultureIgnoreCase)
        };

        return ordered.ToList();
    }
}
