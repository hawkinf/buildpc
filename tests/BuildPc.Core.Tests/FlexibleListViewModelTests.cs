using BuildPc.Core.Models;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Core.Tests;

public sealed class FlexibleListViewModelTests
{
    [Fact]
    public void CategorySelection_FiltersProductsBeforeAdding()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var memory = Product("ram", ComponentCategory.Memory, "Memória", 400m);
        var viewModel = CreateViewModel(processor, memory);

        Assert.Equal(["Processador"], viewModel.ProductPicker.Options.Select(option => option.Name));

        viewModel.SelectedCategory = viewModel.Categories.Single(category =>
            category.Value == ComponentCategory.Memory);

        Assert.Equal(["Memória"], viewModel.ProductPicker.Options.Select(option => option.Name));
        Assert.False(viewModel.CanAdd);
    }

    [Fact]
    public void Add_AllowsRepeatedProductsAndCalculatesIndependentTotal()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var viewModel = CreateViewModel(processor);

        viewModel.Quantity = 2;
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);

        viewModel.Quantity = 3;
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);

        Assert.Equal(2, viewModel.Items.Count);
        Assert.Equal([false, true], viewModel.Items.Select(item => item.IsAlternate));
        Assert.Equal(5, viewModel.TotalItems);
        Assert.Equal(5000m, viewModel.TotalCostValue);
        Assert.True(viewModel.HasItems);
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public void QuantityAndRemoval_RefreshSummary()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var viewModel = CreateViewModel(processor);
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);

        var item = Assert.Single(viewModel.Items);
        item.Quantity = 4;

        Assert.Equal(4, viewModel.TotalItems);
        Assert.Equal(4000m, viewModel.TotalCostValue);

        item.RemoveCommand.Execute(null);

        Assert.Empty(viewModel.Items);
        Assert.Equal(0m, viewModel.TotalCostValue);
        Assert.True(viewModel.IsEmpty);
    }

    [Fact]
    public void AddedItem_AllowsEditingNameDescriptionAndUnitPrice()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var viewModel = CreateViewModel(processor);
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);

        var item = Assert.Single(viewModel.Items);
        item.Name = "Título personalizado\nem duas linhas";
        item.Description = "Descrição editável\nem duas linhas";
        item.PriceText = "1.250,50";
        item.Quantity = 2;

        Assert.Equal("Título personalizado\nem duas linhas", item.Name);
        Assert.Equal("Descrição editável\nem duas linhas", item.Description);
        Assert.Equal(1250.50m, item.UnitPriceValue);
        Assert.Equal(2501m, viewModel.TotalCostValue);
    }

    [Fact]
    public void Margins_UseCategoryOverrideAndGlobalFallback()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var memory = Product("ram", ComponentCategory.Memory, "Memória", 400m);
        var settings = new BusinessSettings
        {
            GlobalMarginPercent = 20m,
            CategoryMargins = new Dictionary<ComponentCategory, decimal>
            {
                [ComponentCategory.Processor] = 35m
            }
        };
        var viewModel = new FlexibleListViewModel(
            [processor, memory],
            [
                new(ComponentCategory.Processor, "Processador"),
                new(ComponentCategory.Memory, "Memória")
            ],
            settings);

        Assert.Equal(1350.90m, viewModel.ProductPicker.Options.Single().Price);
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);
        viewModel.SelectedCategory = viewModel.Categories[1];
        Assert.Equal(480.90m, viewModel.ProductPicker.Options.Single().Price);
        viewModel.ProductPicker.Selected = memory;
        viewModel.AddCommand.Execute(null);

        Assert.Equal(1831.80m, viewModel.TotalPriceValue);
        Assert.Equal(431.80m, viewModel.TotalProfitValue);
        Assert.Equal(35m, viewModel.Items[0].MarginPercent);
        Assert.Equal(20m, viewModel.Items[1].MarginPercent);
    }

    [Fact]
    public void EyeState_ShowsAndHidesCostOnEveryProduct()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var viewModel = CreateViewModel(processor);
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);
        var item = Assert.Single(viewModel.Items);

        viewModel.SetSensitiveTotalsVisible(true);

        Assert.True(viewModel.IsSensitiveTotalsVisible);
        Assert.True(item.IsCostVisible);

        viewModel.SetSensitiveTotalsVisible(false);

        Assert.False(viewModel.IsSensitiveTotalsVisible);
        Assert.False(item.IsCostVisible);
    }

    [Fact]
    public void UpdatingSettings_RefreshesSalePricesInPicker()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var viewModel = CreateViewModel(processor);

        viewModel.ApplySettings(new BusinessSettings { GlobalMarginPercent = 30m });

        Assert.Equal(1300.90m, viewModel.ProductPicker.Options.Single().Price);
    }

    [Fact]
    public void SavedQuote_BecomesUnavailableForExportAfterEditing()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        SavedQuote? persisted = null;
        var viewModel = new FlexibleListViewModel(
            [processor],
            [new(ComponentCategory.Processor, "Processador")],
            new BusinessSettings { GlobalMarginPercent = 10m },
            list => persisted = new SavedQuote
            {
                Id = Guid.NewGuid(),
                Number = 1,
                CreatedAt = DateTimeOffset.Now,
                ClientName = list.ClientName,
                ClientPhone = list.ClientPhone,
                Items = list.BuildQuoteItems(),
                TotalCost = list.TotalCostValue,
                TotalPrice = list.TotalPriceValue
            });
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);
        viewModel.ClientName = "Cliente";
        viewModel.ClientPhone = "11999999999";

        viewModel.SaveQuoteCommand.Execute(null);

        Assert.NotNull(persisted);
        Assert.Equal("(11) 99999-9999", persisted.ClientPhone);
        Assert.True(viewModel.CanExport);
        viewModel.Items[0].Description = "Alterada";
        Assert.False(viewModel.CanExport);
    }

    [Fact]
    public void SellingPrice_IsEditableWithoutChangingHiddenCost()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var viewModel = new FlexibleListViewModel(
            [processor],
            [new(ComponentCategory.Processor, "Processador")],
            new BusinessSettings { GlobalMarginPercent = 20m });
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);

        var item = Assert.Single(viewModel.Items);
        item.SellingPriceText = "1.350,00";

        Assert.Equal(1000m, item.UnitPriceValue);
        Assert.Equal(1350.90m, item.SellingUnitPriceValue);
        Assert.Equal("1.350,90", item.SellingPriceText);
        Assert.Equal(350.90m, viewModel.TotalProfitValue);
        Assert.Equal(35.09m, item.MarginPercent);
    }

    [Fact]
    public void SellingPrice_FormatsManualValueWithThousandsAndTwoDecimals()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var viewModel = CreateViewModel(processor);
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);
        var item = Assert.Single(viewModel.Items);

        item.SellingPriceText = "1350,5";

        Assert.Equal("1.350,90", item.SellingPriceText);
        Assert.Equal(1350.90m, item.SellingUnitPriceValue);
    }

    [Fact]
    public void SellingPrice_AdvancesToNextNinetyEndingWhenNecessary()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var viewModel = CreateViewModel(processor);
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);
        var item = Assert.Single(viewModel.Items);

        item.SellingPriceText = "1350,99";

        Assert.Equal("1.351,90", item.SellingPriceText);
        Assert.Equal(1351.90m, item.SellingUnitPriceValue);
    }

    [Fact]
    public void SellingPrice_CannotProduceProfitBelowFifteenPercent()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var viewModel = new FlexibleListViewModel(
            [processor],
            [new(ComponentCategory.Processor, "Processador")],
            new BusinessSettings { GlobalMarginPercent = 20m });
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);
        var item = Assert.Single(viewModel.Items);

        item.SellingPriceText = "1.050,00";

        Assert.Equal(1150.90m, item.SellingUnitPriceValue);
        Assert.Equal("1.150,90", item.SellingPriceText);
        Assert.Equal(15.09m, item.MarginPercent);
        Assert.Equal(15.09m, viewModel.TotalProfitPercentValue);
        Assert.Equal("15,09%", viewModel.TotalProfitPercent);
    }

    [Fact]
    public void BusinessSettings_ClampPersistedMarginsBelowMinimum()
    {
        var settings = new BusinessSettings
        {
            GlobalMarginPercent = 5m,
            CategoryMargins = new Dictionary<ComponentCategory, decimal>
            {
                [ComponentCategory.Processor] = 10m
            }
        };

        Assert.Equal(15m, settings.MarginFor(ComponentCategory.Processor));
        Assert.Equal(15m, settings.MarginFor(ComponentCategory.Memory));
    }

    private static FlexibleListViewModel CreateViewModel(params PcComponent[] products) =>
        new(
            products,
            [
                new(ComponentCategory.Processor, "Processador"),
                new(ComponentCategory.Memory, "Memória")
            ]);

    private static PcComponent Product(
        string id,
        ComponentCategory category,
        string name,
        decimal price) =>
        new()
        {
            Id = id,
            Category = category,
            Name = name,
            Brand = "Teste",
            Description = "Produto de teste",
            Price = price
        };
}
