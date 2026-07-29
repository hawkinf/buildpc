using BuildPc.Core.Models;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Core.Tests;

public sealed class ProductListItemViewModelTests
{
    [Fact]
    public void SelectCommand_OpensSelectedProductAndExposesCharacteristics()
    {
        ProductListItemViewModel? selected = null;
        var component = new PcComponent
        {
            Id = "cpu-detail",
            Category = ComponentCategory.Processor,
            Name = "Ryzen de teste",
            Brand = "AMD",
            Description = "Produto com características",
            Price = 999.90m,
            PowerWatts = 65,
            Socket = "AM5",
            MemoryType = "DDR5",
            SupportedSockets = new(StringComparer.OrdinalIgnoreCase) { "AM5", "AM4" },
            ImportSource = "kabum"
        };
        var item = ProductListItemViewModel.From(
            component,
            _ => true,
            product => selected = product,
            isAlternate: false);

        item.SelectCommand.Execute(null);

        Assert.Same(item, selected);
        Assert.Equal("65 W", item.PowerText);
        Assert.Equal("AM5", item.SocketText);
        Assert.Equal("DDR5", item.MemoryTypeText);
        Assert.Contains("AM5", item.SupportedSocketsText);
        Assert.Contains("AM4", item.SupportedSocketsText);
        Assert.True(item.IsImported);
    }
}
