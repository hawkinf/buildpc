using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

public sealed class PriceTableRowBuilderTests
{
    [Fact]
    public void Build_AppliesTheSaleMarginWhenShowingSalePrice()
    {
        var settings = new BusinessSettings { GlobalMarginPercent = 20m };
        var catalog = new[] { Product("CPU", ComponentCategory.Processor, 100m) };

        var rows = PriceTableRowBuilder.Build(
            catalog,
            settings,
            categoryFilter: null,
            searchText: null,
            PriceTableSortMode.NameAscending,
            showSalePrice: true);

        var row = Assert.Single(rows);
        Assert.Equal(
            PricingCalculator.CalculateSalePrice(100m, 20m),
            row.Price);
    }

    [Fact]
    public void Build_ShowsTheRawCostWhenNotShowingSalePrice()
    {
        var settings = new BusinessSettings { GlobalMarginPercent = 20m };
        var catalog = new[] { Product("CPU", ComponentCategory.Processor, 100m) };

        var rows = PriceTableRowBuilder.Build(
            catalog,
            settings,
            categoryFilter: null,
            searchText: null,
            PriceTableSortMode.NameAscending,
            showSalePrice: false);

        Assert.Equal(100m, Assert.Single(rows).Price);
    }

    [Fact]
    public void Build_FiltersByCategory()
    {
        var settings = new BusinessSettings();
        var catalog = new[]
        {
            Product("CPU", ComponentCategory.Processor, 100m),
            Product("SSD", ComponentCategory.Storage, 200m)
        };

        var rows = PriceTableRowBuilder.Build(
            catalog,
            settings,
            ComponentCategory.Storage,
            searchText: null,
            PriceTableSortMode.NameAscending,
            showSalePrice: false);

        Assert.Equal("SSD", Assert.Single(rows).Title);
    }

    [Fact]
    public void Build_FiltersBySearchText()
    {
        var settings = new BusinessSettings();
        var catalog = new[]
        {
            Product("Ryzen 7800X3D", ComponentCategory.Processor, 100m),
            Product("Core i7", ComponentCategory.Processor, 100m)
        };

        var rows = PriceTableRowBuilder.Build(
            catalog,
            settings,
            categoryFilter: null,
            "ryzen",
            PriceTableSortMode.NameAscending,
            showSalePrice: false);

        Assert.Equal("Ryzen 7800X3D", Assert.Single(rows).Title);
    }

    [Theory]
    [InlineData(PriceTableSortMode.NameAscending, "A,B")]
    [InlineData(PriceTableSortMode.NameDescending, "B,A")]
    [InlineData(PriceTableSortMode.PriceAscending, "B,A")]
    [InlineData(PriceTableSortMode.PriceDescending, "A,B")]
    public void Build_SortsAccordingToTheRequestedMode(
        PriceTableSortMode sortMode,
        string expectedOrder)
    {
        var settings = new BusinessSettings();
        var catalog = new[]
        {
            Product("A", ComponentCategory.Processor, 200m),
            Product("B", ComponentCategory.Processor, 100m)
        };

        var rows = PriceTableRowBuilder.Build(
            catalog,
            settings,
            categoryFilter: null,
            searchText: null,
            sortMode,
            showSalePrice: false);

        Assert.Equal(expectedOrder.Split(','), rows.Select(row => row.Title));
    }

    [Fact]
    public void Build_UsesTheConfiguredCategoryDisplayName()
    {
        var settings = new BusinessSettings
        {
            ProductCategories =
            [
                new ProductCategoryDefinition
                {
                    Value = ComponentCategory.Processor,
                    Name = "Processadores",
                    DisplayOrder = 1
                }
            ]
        };
        var catalog = new[] { Product("CPU", ComponentCategory.Processor, 100m) };

        var rows = PriceTableRowBuilder.Build(
            catalog,
            settings,
            categoryFilter: null,
            searchText: null,
            PriceTableSortMode.NameAscending,
            showSalePrice: false);

        Assert.Equal("Processadores", Assert.Single(rows).CategoryName);
    }

    private static PcComponent Product(
        string name,
        ComponentCategory category,
        decimal price) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Category = category,
            Name = name,
            Brand = "Teste",
            Description = name,
            Price = price
        };
}
