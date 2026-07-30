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
            list => Task.FromResult<SavedQuote?>(persisted = new SavedQuote
            {
                Id = Guid.NewGuid(),
                Number = 1,
                CreatedAt = DateTimeOffset.Now,
                ClientName = list.ClientName,
                ClientPhone = list.ClientPhone,
                Items = list.BuildQuoteItems(),
                TotalCost = list.TotalCostValue,
                TotalPrice = list.TotalPriceValue
            }));
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

    [Fact]
    public void Quantity_AcceptsValuesAboveTheOldHundredLimit()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 10m);
        var viewModel = CreateViewModel(processor);

        viewModel.Quantity = 250;
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);

        var item = Assert.Single(viewModel.Items);
        Assert.Equal(250, item.Quantity);
        Assert.Equal(2500m, viewModel.TotalCostValue);

        item.Quantity = 1500;
        Assert.Equal(1500, item.Quantity);

        // O teto alto continua existindo só para conter erro de digitação.
        item.Quantity = 999_999;
        Assert.Equal(QuantityRange.Maximum, item.Quantity);
        item.Quantity = 0;
        Assert.Equal(QuantityRange.Minimum, item.Quantity);
    }

    [Fact]
    public void Clear_OnlyWipesTheAssemblyAfterConfirmation()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var viewModel = CreateViewModel(processor);
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);
        viewModel.ClientName = "Cliente";

        viewModel.RequestClearCommand.Execute(null);

        // Pedir para limpar não descarta nada por si só.
        Assert.True(viewModel.IsClearConfirmationVisible);
        Assert.Single(viewModel.Items);
        Assert.Equal("Cliente", viewModel.ClientName);

        viewModel.CancelClearCommand.Execute(null);

        Assert.False(viewModel.IsClearConfirmationVisible);
        Assert.Single(viewModel.Items);

        viewModel.RequestClearCommand.Execute(null);
        viewModel.ConfirmClearCommand.Execute(null);

        Assert.False(viewModel.IsClearConfirmationVisible);
        Assert.Empty(viewModel.Items);
        Assert.Equal(string.Empty, viewModel.ClientName);
        Assert.Null(viewModel.SavedQuote);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public void RequestClear_DoesNothingWhenThereIsNothingToLose()
    {
        var viewModel = CreateViewModel(
            Product("cpu", ComponentCategory.Processor, "Processador", 1000m));

        viewModel.RequestClearCommand.Execute(null);

        Assert.False(viewModel.IsClearConfirmationVisible);
    }

    [Fact]
    public void ClearConfirmationMessage_MentionsTheSavedQuoteWhenThereIsOne()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var viewModel = CreateViewModel(processor);
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);

        Assert.Contains("não está gravado", viewModel.ClearConfirmationMessage);

        viewModel.LoadQuote(new SavedQuote
        {
            Id = Guid.NewGuid(),
            Number = 31,
            CreatedAt = DateTimeOffset.Now,
            ClientName = "Lia",
            ClientPhone = "(11) 95555-0000",
            Items = []
        });

        Assert.Contains("#000031", viewModel.ClearConfirmationMessage);
    }

    [Fact]
    public void LoadQuote_RestoresTheAgreedValuesAndIsReadyToExport()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var viewModel = CreateViewModel(processor);
        var quote = new SavedQuote
        {
            Id = Guid.NewGuid(),
            Number = 42,
            CreatedAt = DateTimeOffset.Now,
            ClientName = "Joana",
            ClientPhone = "(11) 98888-0000",
            Notes = "Entrega combinada",
            Items =
            [
                new SavedQuoteItem
                {
                    ComponentId = "cpu",
                    Category = ComponentCategory.Processor,
                    CategoryName = "Processador",
                    Name = "Título negociado",
                    Description = "Descrição negociada",
                    Quantity = 3,
                    UnitCost = 900m,
                    MarginPercent = 40m,
                    UnitPrice = 1499.90m
                }
            ]
        };

        viewModel.LoadQuote(quote);

        var item = Assert.Single(viewModel.Items);
        Assert.Equal("Título negociado", item.Name);
        Assert.Equal("Descrição negociada", item.Description);
        Assert.Equal(3, item.Quantity);
        // Os valores acordados prevalecem sobre o catálogo e a margem atuais.
        Assert.Equal(900m, item.UnitPriceValue);
        Assert.Equal(1499.90m, item.SellingUnitPriceValue);
        Assert.Equal(4499.70m, viewModel.TotalPriceValue);
        Assert.Equal(2700m, viewModel.TotalCostValue);
        Assert.Equal("Joana", viewModel.ClientName);
        Assert.Equal("Entrega combinada", viewModel.Notes);
        // Reabrir não é alteração pendente: já pode exportar.
        Assert.False(viewModel.IsDirty);
        Assert.True(viewModel.CanExport);
        Assert.Equal(quote, viewModel.SavedQuote);
    }

    [Fact]
    public void LoadQuote_KeepsItemsWhoseProductWasDeletedFromTheCatalog()
    {
        var viewModel = CreateViewModel(
            Product("cpu", ComponentCategory.Processor, "Processador", 1000m));
        var quote = new SavedQuote
        {
            Id = Guid.NewGuid(),
            Number = 7,
            CreatedAt = DateTimeOffset.Now,
            ClientName = "Pedro",
            ClientPhone = "(11) 97777-0000",
            Items =
            [
                new SavedQuoteItem
                {
                    ComponentId = "produto-apagado",
                    Category = ComponentCategory.Memory,
                    CategoryName = "Memória",
                    Name = "Memória removida",
                    Description = "Não está mais no catálogo",
                    Quantity = 2,
                    UnitCost = 200m,
                    MarginPercent = 30m,
                    UnitPrice = 279.90m
                }
            ]
        };

        viewModel.LoadQuote(quote);

        var item = Assert.Single(viewModel.Items);
        Assert.Equal("Memória removida", item.Name);
        Assert.Equal("Memória", item.CategoryName);
        Assert.Equal(279.90m, item.SellingUnitPriceValue);
        Assert.Equal(559.80m, viewModel.TotalPriceValue);
    }

    [Fact]
    public void LoadQuote_ReplacesWhateverWasAlreadyInTheAssembly()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var viewModel = CreateViewModel(processor);
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);
        Assert.Single(viewModel.Items);

        viewModel.LoadQuote(new SavedQuote
        {
            Id = Guid.NewGuid(),
            Number = 9,
            CreatedAt = DateTimeOffset.Now,
            ClientName = "Rita",
            ClientPhone = "(11) 96666-0000",
            Items = []
        });

        Assert.Empty(viewModel.Items);
        Assert.Equal("Rita", viewModel.ClientName);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public void Discount_ReducesTheFinalPriceAndFeedsTheDraft()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 100m);
        var viewModel = CreateViewModel(processor);
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);
        var total = viewModel.TotalPriceValue;

        viewModel.DiscountText = "30,00";
        viewModel.ValidityDays = 20;
        viewModel.PaymentTerms = "2x no cartão";
        viewModel.DeliveryTerms = "3 dias úteis";

        Assert.Equal(30m, viewModel.DiscountValue);
        Assert.True(viewModel.HasDiscount);
        Assert.Equal(total - 30m, viewModel.FinalPriceValue);

        var draft = viewModel.BuildQuoteDraft(new BusinessSettings());
        Assert.Equal(30m, draft.DiscountAmount);
        Assert.Equal(20, draft.ValidityDays);
        Assert.Equal("2x no cartão", draft.PaymentTerms);
        Assert.Equal("3 dias úteis", draft.DeliveryTerms);
    }

    [Fact]
    public void InvalidDiscountTextIsTreatedAsNoDiscount()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 100m);
        var viewModel = CreateViewModel(processor);
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);

        viewModel.DiscountText = "abc";
        Assert.Equal(0m, viewModel.DiscountValue);

        viewModel.DiscountText = "-50";
        Assert.Equal(0m, viewModel.DiscountValue);
        Assert.Equal(viewModel.TotalPriceValue, viewModel.FinalPriceValue);
    }

    [Fact]
    public void ClearAndLoadQuoteResetTheCommercialTerms()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 100m);
        var viewModel = CreateViewModel(processor);
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);
        viewModel.DiscountText = "25,00";
        viewModel.ValidityDays = 30;
        viewModel.PaymentTerms = "À vista";

        viewModel.RequestClearCommand.Execute(null);
        viewModel.ConfirmClearCommand.Execute(null);

        Assert.Equal(0m, viewModel.DiscountValue);
        Assert.Equal(0, viewModel.ValidityDays);
        Assert.Equal(string.Empty, viewModel.PaymentTerms);

        viewModel.LoadQuote(new SavedQuote
        {
            Id = Guid.NewGuid(),
            Number = 5,
            CreatedAt = DateTimeOffset.Now,
            ClientName = "Volta",
            ClientPhone = "(11) 93333-0000",
            Items = [],
            DiscountAmount = 40m,
            ValidityDays = 7,
            PaymentTerms = "Boleto",
            DeliveryTerms = "Imediata"
        });

        Assert.Equal(40m, viewModel.DiscountValue);
        Assert.Equal(7, viewModel.ValidityDays);
        Assert.Equal("Boleto", viewModel.PaymentTerms);
        Assert.Equal("Imediata", viewModel.DeliveryTerms);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public void LoadQuoteAsCopy_KeepsItemsButDropsTheOriginalNumberAndClient()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m);
        var viewModel = CreateViewModel(processor);
        var original = new SavedQuote
        {
            Id = Guid.NewGuid(),
            Number = 101,
            CreatedAt = DateTimeOffset.Now,
            ClientName = "Cliente Original",
            ClientPhone = "(11) 94444-0000",
            Notes = "Observação mantida",
            DiscountAmount = 50m,
            ValidityDays = 12,
            PaymentTerms = "2x",
            Items =
            [
                new SavedQuoteItem
                {
                    ComponentId = "cpu",
                    Category = ComponentCategory.Processor,
                    CategoryName = "Processador",
                    Name = "CPU acordada",
                    Description = "Descrição acordada",
                    Quantity = 2,
                    UnitCost = 900m,
                    MarginPercent = 30m,
                    UnitPrice = 1200m
                }
            ]
        };

        viewModel.LoadQuoteAsCopy(original);

        // Itens e condições vêm juntos, para não remontar a venda.
        var item = Assert.Single(viewModel.Items);
        Assert.Equal("CPU acordada", item.Name);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(1200m, item.SellingUnitPriceValue);
        Assert.Equal(50m, viewModel.DiscountValue);
        Assert.Equal(12, viewModel.ValidityDays);
        Assert.Equal("2x", viewModel.PaymentTerms);
        Assert.Equal("Observação mantida", viewModel.Notes);

        // Sem vínculo com o original: gravar cria número novo.
        Assert.Null(viewModel.SavedQuote);
        Assert.Equal(string.Empty, viewModel.ClientName);
        Assert.Equal(string.Empty, viewModel.ClientPhone);
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.CanExport);
        Assert.Contains("#000101", viewModel.StatusMessage);
    }

    [Fact]
    public void Add_MismatchedSocketRaisesCompatibilityIssue()
    {
        var processor = Product("cpu", ComponentCategory.Processor, "Processador", 1000m)
            with
        { Socket = "AM5" };
        var motherboard = Product("mb", ComponentCategory.Motherboard, "Placa-mãe", 800m)
            with
        { Socket = "LGA1700" };
        var viewModel = new FlexibleListViewModel(
            [processor, motherboard],
            [
                new(ComponentCategory.Processor, "Processador"),
                new(ComponentCategory.Motherboard, "Placa-mãe")
            ]);

        Assert.Empty(viewModel.CompatibilityIssues);

        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);

        viewModel.SelectedCategory = viewModel.Categories.Single(category =>
            category.Value == ComponentCategory.Motherboard);
        viewModel.ProductPicker.Selected = motherboard;
        viewModel.AddCommand.Execute(null);

        Assert.True(viewModel.HasCompatibilityIssues);
        Assert.Contains(
            viewModel.CompatibilityIssues,
            issue => issue.Severity == IssueSeverity.Error &&
                     issue.Message.Contains("Soquetes incompatíveis"));
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
