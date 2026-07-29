using BuildPc.Core.Models;
using BuildPc.Desktop.Services;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Core.Tests;

public sealed class ProductCatalogSorterTests
{
    [Theory]
    [InlineData(
        ProductCatalogSortMode.DescriptionAscending,
        "Produto A,Produto Z,Produto M")]
    [InlineData(
        ProductCatalogSortMode.DescriptionDescending,
        "Produto M,Produto Z,Produto A")]
    [InlineData(
        ProductCatalogSortMode.PriceAscending,
        "Produto A,Produto M,Produto Z")]
    [InlineData(
        ProductCatalogSortMode.PriceDescending,
        "Produto Z,Produto M,Produto A")]
    public void PriceTableRows_PreserveTheCatalogSorting(
        ProductCatalogSortMode mode,
        string expectedNames)
    {
        var sortedCatalog = ProductCatalogSorter.Sort(CreateProducts(), mode);

        var exportedRows = ProductPriceTableRowFactory.Create(
            sortedCatalog,
            component => component.Price);

        Assert.Equal(
            expectedNames.Split(','),
            exportedRows.Select(row => row.Title));
    }

    private static IReadOnlyList<ProductListItemViewModel> CreateProducts() =>
    [
        Product("Produto Z", "Beta", 300m),
        Product("Produto A", "Alfa", 100m),
        Product("Produto M", "Gama", 200m)
    ];

    private static ProductListItemViewModel Product(
        string name,
        string description,
        decimal price) =>
        ProductListItemViewModel.From(
            new PcComponent
            {
                Id = Guid.NewGuid().ToString("N"),
                Category = ComponentCategory.Storage,
                Name = name,
                Brand = "Teste",
                Description = description,
                Price = price
            },
            _ => true,
            _ => { },
            isAlternate: false);
}
