using BuildPc.Core.Models;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Core.Tests;

public sealed class ComponentSlotViewModelTests
{
    [Fact]
    public void HeaderCommands_ToggleAlphabeticalAndPriceOrdering()
    {
        var slot = CreateSlot();

        Assert.Equal(["Alpha", "Beta", "Gamma"], slot.Options.Select(option => option.Name));

        slot.SortByNameCommand.Execute(null);
        Assert.Equal(["Gamma", "Beta", "Alpha"], slot.Options.Select(option => option.Name));

        slot.SortByPriceCommand.Execute(null);
        Assert.Equal([100m, 200m, 300m], slot.Options.Select(option => option.Price));

        slot.SortByPriceCommand.Execute(null);
        Assert.Equal([300m, 200m, 100m], slot.Options.Select(option => option.Price));
    }

    [Fact]
    public void Filter_ShowsOnlyMatchesWithoutRemovingCurrentSelection()
    {
        var slot = CreateSlot();
        slot.Selected = slot.Options.Single(option => option.Name == "Alpha").Component;

        slot.FilterText = "Gamma";

        Assert.Equal(["Gamma"], slot.Options.Select(option => option.Name));
        Assert.Equal("Alpha", slot.Selected?.Name);
    }

    private static ComponentSlotViewModel CreateSlot() =>
        new(
            "processor",
            ComponentCategory.Processor,
            "Processador",
            "Teste",
            "CPU",
            [
                Product("a", "Alpha", 300m),
                Product("b", "Beta", 100m),
                Product("c", "Gamma", 200m)
            ],
            _ => { });

    private static PcComponent Product(string id, string name, decimal price) =>
        new()
        {
            Id = id,
            Category = ComponentCategory.Processor,
            Name = name,
            Brand = "Teste",
            Description = "Componente de teste",
            Price = price
        };
}
