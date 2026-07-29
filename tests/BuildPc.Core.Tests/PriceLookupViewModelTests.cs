using BuildPc.Core.Models;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Core.Tests;

public sealed class PriceLookupViewModelTests
{
    [Fact]
    public void StartsWithSalePricesAndSupportsCostSelection()
    {
        var viewModel = CreateViewModel();

        Assert.True(viewModel.IsSaleActive);
        Assert.Equal("PREÇO DE VENDA", viewModel.PriceColumnTitle);
        Assert.Equal(
            120.90m,
            viewModel.Items.Single(product =>
                product.Name == "SSD Zulu").DisplayPriceValue);

        viewModel.ShowCostCommand.Execute(null);

        Assert.True(viewModel.IsCostActive);
        Assert.Equal("CUSTO", viewModel.PriceColumnTitle);
        Assert.Equal(
            100m,
            viewModel.Items.Single(product =>
                product.Name == "SSD Zulu").DisplayPriceValue);
    }

    [Fact]
    public void FiltersByCategoryAndTogglesTitleAndPriceOrdering()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedCategory = viewModel.Categories.Single(category =>
            category.Value == ComponentCategory.Storage);

        Assert.Equal(
            ["SSD Alfa", "SSD Zulu"],
            viewModel.Items.Select(product => product.Name));

        viewModel.SortByTitleCommand.Execute(null);

        Assert.Equal(
            ["SSD Zulu", "SSD Alfa"],
            viewModel.Items.Select(product => product.Name));

        viewModel.SortByPriceCommand.Execute(null);

        Assert.Equal(
            ["SSD Zulu", "SSD Alfa"],
            viewModel.Items.Select(product => product.Name));
    }

    [Fact]
    public void SearchRequiresAllKeywordsLikeTheOtherProductFilters()
    {
        var viewModel = CreateViewModel();

        viewModel.SearchText = "ssd alfa";

        var product = Assert.Single(viewModel.Items);
        Assert.Equal("SSD Alfa", product.Name);
    }

    private static PriceLookupViewModel CreateViewModel() =>
        new(
            [
                Product("SSD Zulu", ComponentCategory.Storage, 100m),
                Product("SSD Alfa", ComponentCategory.Storage, 200m),
                Product("CPU Beta", ComponentCategory.Processor, 300m)
            ],
            ProductCategoryDefinition.Defaults(),
            new BusinessSettings { GlobalMarginPercent = 20m });

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
