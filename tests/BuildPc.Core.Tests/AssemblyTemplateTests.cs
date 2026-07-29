using BuildPc.Core.Models;
using BuildPc.Core.Services;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Core.Tests;

public sealed class AssemblyTemplateTests
{
    [Fact]
    public void TemplateRoundTripsThroughSqlite()
    {
        using var workspace = new Workspace();

        var saved = workspace.Repository.SaveTemplate(new AssemblyTemplate
        {
            Name = "PC Gamer básico",
            Description = "Combinação mais vendida",
            Items =
            [
                new AssemblyTemplateItem
                {
                    ComponentId = "cpu",
                    Category = ComponentCategory.Processor,
                    Quantity = 1
                },
                new AssemblyTemplateItem
                {
                    ComponentId = "ram",
                    Category = ComponentCategory.Memory,
                    Quantity = 2
                }
            ]
        });

        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal(3, saved.TotalItems);

        var loaded = Assert.Single(workspace.Repository.GetTemplates());
        Assert.Equal("PC Gamer básico", loaded.Name);
        Assert.Equal("Combinação mais vendida", loaded.Description);
        Assert.Equal(2, loaded.Items.Count);
        Assert.Equal(3, loaded.TotalItems);
    }

    [Fact]
    public void SavingWithTheSameIdUpdatesInsteadOfDuplicating()
    {
        using var workspace = new Workspace();
        var saved = workspace.Repository.SaveTemplate(Template("Original"));

        workspace.Repository.SaveTemplate(saved with { Name = "Renomeado" });

        var loaded = Assert.Single(workspace.Repository.GetTemplates());
        Assert.Equal("Renomeado", loaded.Name);
        Assert.Equal(saved.Id, loaded.Id);
    }

    [Fact]
    public void TemplateWithoutNameOrItemsIsRejected()
    {
        using var workspace = new Workspace();

        Assert.Throws<ArgumentException>(() =>
            workspace.Repository.SaveTemplate(Template("   ")));
        Assert.Throws<ArgumentException>(() =>
            workspace.Repository.SaveTemplate(new AssemblyTemplate
            {
                Name = "Sem itens",
                Items = []
            }));
    }

    [Fact]
    public void DeleteRemovesOnlyTheRequestedTemplate()
    {
        using var workspace = new Workspace();
        var first = workspace.Repository.SaveTemplate(Template("Primeiro"));
        workspace.Repository.SaveTemplate(Template("Segundo"));

        Assert.True(workspace.Repository.DeleteTemplate(first.Id));

        var remaining = Assert.Single(workspace.Repository.GetTemplates());
        Assert.Equal("Segundo", remaining.Name);
        Assert.False(workspace.Repository.DeleteTemplate(first.Id));
    }

    [Fact]
    public void ApplyTemplateUsesCurrentCatalogPricesNotFrozenValues()
    {
        var processor = Product("cpu", ComponentCategory.Processor, 1000m);
        var viewModel = new FlexibleListViewModel(
            [processor],
            [new(ComponentCategory.Processor, "Processador")],
            new BusinessSettings { GlobalMarginPercent = 20m });

        viewModel.ApplyTemplate(
            new AssemblyTemplate
            {
                Name = "Modelo",
                Items =
                [
                    new AssemblyTemplateItem
                    {
                        ComponentId = "cpu",
                        Category = ComponentCategory.Processor,
                        Quantity = 3
                    }
                ]
            },
            [processor]);

        var item = Assert.Single(viewModel.Items);
        Assert.Equal(3, item.Quantity);
        // O custo vem do catálogo e a venda é recalculada pela margem atual.
        Assert.Equal(1000m, item.UnitPriceValue);
        Assert.Equal(20m, item.MarginPercent);
        Assert.Null(viewModel.SavedQuote);
        Assert.True(viewModel.IsDirty);
        Assert.Contains("Modelo", viewModel.StatusMessage);
    }

    [Fact]
    public void ApplyTemplateSkipsProductsThatLeftTheCatalogAndWarns()
    {
        var processor = Product("cpu", ComponentCategory.Processor, 1000m);
        var viewModel = new FlexibleListViewModel(
            [processor],
            [new(ComponentCategory.Processor, "Processador")]);

        viewModel.ApplyTemplate(
            new AssemblyTemplate
            {
                Name = "Modelo",
                Items =
                [
                    new AssemblyTemplateItem { ComponentId = "cpu", Quantity = 1 },
                    new AssemblyTemplateItem { ComponentId = "apagado", Quantity = 1 }
                ]
            },
            [processor]);

        Assert.Single(viewModel.Items);
        Assert.False(viewModel.IsStatusSuccess);
        Assert.Contains("já não está no catálogo", viewModel.StatusMessage);
    }

    [Fact]
    public void BuildTemplateItemsCapturesTheCurrentAssembly()
    {
        var processor = Product("cpu", ComponentCategory.Processor, 1000m);
        var viewModel = new FlexibleListViewModel(
            [processor],
            [new(ComponentCategory.Processor, "Processador")]);
        viewModel.Quantity = 4;
        viewModel.ProductPicker.Selected = processor;
        viewModel.AddCommand.Execute(null);

        var items = viewModel.BuildTemplateItems();

        var item = Assert.Single(items);
        Assert.Equal("cpu", item.ComponentId);
        Assert.Equal(4, item.Quantity);
        Assert.Equal(ComponentCategory.Processor, item.Category);
    }

    private static AssemblyTemplate Template(string name) =>
        new()
        {
            Name = name,
            Items =
            [
                new AssemblyTemplateItem
                {
                    ComponentId = "cpu",
                    Category = ComponentCategory.Processor,
                    Quantity = 1
                }
            ]
        };

    private static PcComponent Product(
        string id,
        ComponentCategory category,
        decimal price) =>
        new()
        {
            Id = id,
            Category = category,
            Name = $"Produto {id}",
            Brand = "Teste",
            Description = "Produto de teste",
            Price = price
        };

    private sealed class Workspace : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-modelos-{Guid.NewGuid():N}");

        public Workspace()
        {
            Directory.CreateDirectory(_root);
            Repository = new QuoteRepository(Path.Combine(_root, "catalogo.db"));
        }

        public QuoteRepository Repository { get; }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
