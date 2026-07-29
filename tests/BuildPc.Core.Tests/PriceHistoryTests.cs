using BuildPc.Core.Models;
using BuildPc.Core.Services;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Core.Tests;

public sealed class PriceHistoryTests
{
    [Fact]
    public void ImportRecordsOnlyRealPriceChanges()
    {
        using var workspace = new Workspace();
        var repository = workspace.Repository;

        repository.ReplaceImported(
            ComponentCategory.Processor,
            "kabum",
            [Product("kabum-1", 100m), Product("kabum-2", 200m)]);

        // Segunda carga: um sobe, o outro não muda.
        var result = repository.ReplaceImported(
            ComponentCategory.Processor,
            "kabum",
            [Product("kabum-1", 130m), Product("kabum-2", 200m)]);

        Assert.True(result.HasPriceChanges);
        var change = Assert.Single(result.PriceChanges);
        Assert.Equal("kabum-1", change.ComponentId);
        Assert.Equal(100m, change.PreviousPrice);
        Assert.Equal(130m, change.CurrentPrice);
        Assert.True(change.IsIncrease);
        Assert.Equal(30m, change.Difference);
        Assert.Equal(30m, change.PercentChange);
        Assert.Equal(1, result.Increases);
        Assert.Equal(0, result.Decreases);

        // Sem mudança de preço não há linha no histórico.
        Assert.Empty(repository.GetPriceHistory("kabum-2"));
        Assert.Single(repository.GetPriceHistory("kabum-1"));
    }

    [Fact]
    public void PriceDropIsRecordedAsADecrease()
    {
        using var workspace = new Workspace();
        var repository = workspace.Repository;
        repository.ReplaceImported(
            ComponentCategory.Processor,
            "kabum",
            [Product("kabum-1", 200m)]);

        var result = repository.ReplaceImported(
            ComponentCategory.Processor,
            "kabum",
            [Product("kabum-1", 150m)]);

        var change = Assert.Single(result.PriceChanges);
        Assert.False(change.IsIncrease);
        Assert.Equal(-50m, change.Difference);
        Assert.Equal(-25m, change.PercentChange);
        Assert.Equal(1, result.Decreases);
    }

    [Fact]
    public void HistoryIsOrderedFromNewestToOldest()
    {
        using var workspace = new Workspace();
        var repository = workspace.Repository;

        foreach (var price in new[] { 100m, 120m, 90m, 140m })
        {
            repository.ReplaceImported(
                ComponentCategory.Processor,
                "kabum",
                [Product("kabum-1", price)]);
        }

        var history = repository.GetPriceHistory("kabum-1");

        // Três mudanças: 100→120, 120→90, 90→140.
        Assert.Equal(3, history.Count);
        Assert.Equal([90m, 120m, 100m], history.Select(entry => entry.Price));
    }

    [Fact]
    public void DisappearedProductsAreCounted()
    {
        using var workspace = new Workspace();
        var repository = workspace.Repository;
        repository.ReplaceImported(
            ComponentCategory.Processor,
            "kabum",
            [Product("kabum-1", 100m), Product("kabum-2", 200m)]);

        // A loja deixou de trazer o segundo item.
        var result = repository.ReplaceImported(
            ComponentCategory.Processor,
            "kabum",
            [Product("kabum-1", 100m)]);

        Assert.Equal(1, result.Disappeared);
    }

    [Fact]
    public void ViewModelItemShowsTheVariationAgainstTheNextPrice()
    {
        var entry = new PriceHistoryEntry
        {
            ComponentId = "kabum-1",
            Price = 100m,
            RecordedAt = DateTimeOffset.Now,
            Source = "kabum"
        };

        var increase = new PriceHistoryItemViewModel(entry, priceAfterThisChange: 130m);
        Assert.True(increase.IsIncrease);
        Assert.False(increase.IsDecrease);
        Assert.Contains("+", increase.Difference);
        Assert.Contains("30,00%", increase.PercentChange);

        var decrease = new PriceHistoryItemViewModel(entry, priceAfterThisChange: 80m);
        Assert.True(decrease.IsDecrease);
        Assert.False(decrease.IsIncrease);
        Assert.StartsWith("-", decrease.Difference, StringComparison.Ordinal);

        var unchanged = new PriceHistoryItemViewModel(entry, priceAfterThisChange: 100m);
        Assert.False(unchanged.HasChange);
        Assert.Equal(string.Empty, unchanged.Difference);
    }

    private static PcComponent Product(string id, decimal price) =>
        new()
        {
            Id = id,
            Category = ComponentCategory.Processor,
            Name = $"Processador {id}",
            Brand = "Teste",
            Description = "Produto de teste",
            Price = price,
            ImportSource = "kabum"
        };

    private sealed class Workspace : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-historico-{Guid.NewGuid():N}");

        public Workspace()
        {
            Directory.CreateDirectory(_root);
            Repository = new ComponentCatalogRepository(
                Path.Combine(_root, "catalogo.db"));
        }

        public ComponentCatalogRepository Repository { get; }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
