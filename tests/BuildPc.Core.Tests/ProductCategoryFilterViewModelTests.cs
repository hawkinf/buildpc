using BuildPc.Core.Models;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Core.Tests;

public sealed class ProductCategoryFilterViewModelTests
{
    [Fact]
    public void DisplayNameShowsTotalAndOptionalFilteredCount()
    {
        var category = new ProductCategoryFilterViewModel(
            ComponentCategory.Storage,
            "SSDs / NVMe");

        category.UpdateCounts(115, 115, false);
        Assert.Equal("SSDs / NVMe (115)", category.DisplayName);

        category.UpdateCounts(115, 8, true);
        Assert.Equal("SSDs / NVMe (115) (8)", category.DisplayName);
    }
}
