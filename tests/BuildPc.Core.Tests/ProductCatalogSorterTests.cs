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

    [Theory]
    [InlineData(
        ProductCatalogSortMode.DescriptionAscending,
        "CPU rápida,CPU econômica",
        "SSD econômico,SSD rápido")]
    [InlineData(
        ProductCatalogSortMode.DescriptionDescending,
        "CPU econômica,CPU rápida",
        "SSD rápido,SSD econômico")]
    [InlineData(
        ProductCatalogSortMode.PriceAscending,
        "CPU econômica,CPU rápida",
        "SSD econômico,SSD rápido")]
    [InlineData(
        ProductCatalogSortMode.PriceDescending,
        "CPU rápida,CPU econômica",
        "SSD rápido,SSD econômico")]
    public void CostTableSections_GroupByCategoryAndPreserveSortingWithinEachGroup(
        ProductCatalogSortMode mode,
        string expectedProcessors,
        string expectedStorage)
    {
        var products = new[]
        {
            Product(
                "SSD rápido",
                "Gama",
                300m,
                ComponentCategory.Storage),
            Product(
                "CPU econômica",
                "Zebra",
                100m,
                ComponentCategory.Processor),
            Product(
                "SSD econômico",
                "Beta",
                200m,
                ComponentCategory.Storage),
            Product(
                "CPU rápida",
                "Alfa",
                400m,
                ComponentCategory.Processor)
        };
        var sortedCatalog = ProductCatalogSorter.Sort(products, mode);
        var rows = ProductPriceTableRowFactory.Create(
            sortedCatalog,
            component => component.Price);
        var document = new ProductPriceTableDocument(
            "Tabela de custo",
            "Custo",
            "Todos",
            string.Empty,
            "BuildPC",
            DateTimeOffset.Now,
            rows,
            GroupByCategory: true);

        var sections = ProductPriceTableSectionFactory.Create(document);

        Assert.Equal(
            ["Processador", "SSD / NVMe"],
            sections.Select(section => section.CategoryName));
        Assert.Equal(
            expectedProcessors.Split(','),
            sections[0].Rows.Select(row => row.Title));
        Assert.Equal(
            expectedStorage.Split(','),
            sections[1].Rows.Select(row => row.Title));
    }

    [Fact]
    public void SaleTableKeepsOneUngroupedSection()
    {
        var rows = ProductPriceTableRowFactory.Create(
            ProductCatalogSorter.Sort(
                CreateProducts(),
                ProductCatalogSortMode.PriceDescending),
            component => component.Price);
        var document = new ProductPriceTableDocument(
            "Tabela de venda",
            "Venda",
            "Todos",
            string.Empty,
            "BuildPC",
            DateTimeOffset.Now,
            rows);

        var section = Assert.Single(
            ProductPriceTableSectionFactory.Create(document));

        Assert.Null(section.CategoryName);
        Assert.Equal(
            ["Produto Z", "Produto M", "Produto A"],
            section.Rows.Select(row => row.Title));
    }

    [Fact]
    public void PriceSorting_UsesTheDisplayedPriceSelector()
    {
        var products = new[]
        {
            Product("Produto A", "Alfa", 100m),
            Product("Produto B", "Beta", 200m)
        };

        var sorted = ProductCatalogSorter.Sort(
            products,
            ProductCatalogSortMode.PriceAscending,
            product => product.Name == "Produto A" ? 300m : 250m);

        Assert.Equal(
            ["Produto B", "Produto A"],
            sorted.Select(product => product.Name));
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
        decimal price,
        ComponentCategory category = ComponentCategory.Storage) =>
        ProductListItemViewModel.From(
            new PcComponent
            {
                Id = Guid.NewGuid().ToString("N"),
                Category = category,
                Name = name,
                Brand = "Teste",
                Description = description,
                Price = price
            },
            _ => true,
            _ => { },
            isAlternate: false);
}
