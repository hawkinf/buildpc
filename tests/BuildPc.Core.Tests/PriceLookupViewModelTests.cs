using BuildPc.Core.Models;
using BuildPc.Desktop.Services;
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
        Assert.Equal("Todos (3) (1)", viewModel.Categories[0].DisplayName);
        var storage = viewModel.Categories.Single(category =>
            category.Value == ComponentCategory.Storage);
        Assert.Equal(2, storage.TotalCount);
        Assert.Equal(1, storage.FilteredCount);
        Assert.Equal(
            $"{storage.Name} (2) (1)",
            storage.DisplayName);
    }

    [Fact]
    public void CategoryCountsIgnoreTheSelectedCategoryAndCoverEveryCategory()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedCategory = viewModel.Categories.Single(category =>
            category.Value == ComponentCategory.Processor);

        viewModel.SearchText = "ssd";

        // A categoria selecionada limita a lista, nunca as contagens.
        Assert.Empty(viewModel.Items);
        Assert.Equal("Todos (3) (2)", viewModel.Categories[0].DisplayName);
        var storage = viewModel.Categories.Single(category =>
            category.Value == ComponentCategory.Storage);
        Assert.Equal(2, storage.TotalCount);
        Assert.Equal(2, storage.FilteredCount);
        var processor = viewModel.Categories.Single(category =>
            category.Value == ComponentCategory.Processor);
        Assert.Equal(1, processor.TotalCount);
        Assert.Equal(0, processor.FilteredCount);
        var emptyCategory = viewModel.Categories.Single(category =>
            category.Value == ComponentCategory.Monitor);
        Assert.Equal(0, emptyCategory.TotalCount);
        Assert.Equal(0, emptyCategory.FilteredCount);
    }

    [Fact]
    public void ClearingTheSearchRestoresTotalsInEveryCategory()
    {
        var viewModel = CreateViewModel();
        viewModel.SearchText = "ssd alfa";

        viewModel.SearchText = string.Empty;

        Assert.Equal(3, viewModel.Items.Count);
        var storage = viewModel.Categories.Single(category =>
            category.Value == ComponentCategory.Storage);
        Assert.Equal(2, storage.TotalCount);
        Assert.Equal(2, storage.FilteredCount);
        Assert.Equal($"{storage.Name} (2)", storage.DisplayName);
    }

    private static PriceLookupViewModel CreateViewModel() =>
        new(
            [
                Product("SSD Zulu", ComponentCategory.Storage, 100m),
                Product("SSD Alfa", ComponentCategory.Storage, 200m),
                Product("CPU Beta", ComponentCategory.Processor, 300m)
            ],
            ProductCategoryDefinition.Defaults(),
            new BusinessSettings { GlobalMarginPercent = 20m },
            // Sem laço de mensagens do Avalonia o temporizador nunca dispara.
            action => new ImmediateDebouncer(action));

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
