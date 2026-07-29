using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

public sealed class ProductFilterTests
{
    private static readonly PcComponent Product = new()
    {
        Id = "cpu-7800x3d",
        Category = ComponentCategory.Processor,
        Name = "Processador AMD Ryzen 7 7800X3D",
        Brand = "AMD",
        Description = "8 núcleos e 16 threads",
        Price = 2499.90m,
        Socket = "AM5",
        MemoryType = "DDR5"
    };

    [Theory]
    [InlineData("7800x3d")]
    [InlineData("AMD")]
    [InlineData("16 threads")]
    [InlineData("AM5")]
    public void PlainText_SearchesAllRelevantProductFields(string filter)
    {
        Assert.True(ProductFilter.Matches(Product, filter));
    }

    [Theory]
    [InlineData("Ryzen*")]
    [InlineData("*7800X3D")]
    [InlineData("Ryzen ? 7800X3D")]
    [InlineData("DDR?")]
    public void Wildcards_MatchAnySequenceOrSingleCharacter(string filter)
    {
        Assert.True(ProductFilter.Matches(Product, filter));
    }

    [Theory]
    [InlineData("Intel*")]
    [InlineData("LGA*")]
    [InlineData("Ryzen 9*")]
    public void NonMatchingPatterns_AreRejected(string filter)
    {
        Assert.False(ProductFilter.Matches(Product, filter));
    }

    [Fact]
    public void HighlightRanges_IgnoreWildcardsAndFindLiteralMatches()
    {
        const string text = "Processador AMD Ryzen AM4";

        var ranges = ProductFilter.GetHighlightRanges(text, "*amd*am?");
        var highlighted = ranges
            .Select(range => text.Substring(range.Start, range.Length))
            .ToList();

        Assert.Equal(["AMD", "AM"], highlighted);
    }

    [Fact]
    public void HighlightRanges_AreCaseInsensitive()
    {
        const string text = "Socket AM4 e plataforma am4";

        var ranges = ProductFilter.GetHighlightRanges(text, "Am4");

        Assert.Equal(2, ranges.Count);
        Assert.All(
            ranges,
            range => Assert.Equal(
                "am4",
                text.Substring(range.Start, range.Length),
                ignoreCase: true));
    }

    [Fact]
    public void NegativeWildcard_ExcludesMatchingProducts()
    {
        var notebook = Product with
        {
            Id = "notebook",
            Name = "Notebook AMD Ryzen 7"
        };

        Assert.False(ProductFilter.Matches(notebook, "-note*"));
        Assert.True(ProductFilter.Matches(Product, "-note*"));
    }

    [Fact]
    public void PositiveAndNegativeTerms_CanBeCombined()
    {
        Assert.True(ProductFilter.Matches(Product, "Ryzen* -intel*"));
        Assert.False(ProductFilter.Matches(Product, "Ryzen* -amd"));
    }

    [Fact]
    public void MultipleKeywords_AllMustMatchInAnyOrder()
    {
        var storage = Product with
        {
            Category = ComponentCategory.Storage,
            Name = "SSD Adata Legend 710",
            Brand = "Adata",
            Description = "Armazenamento NVMe de 256 GB"
        };

        Assert.True(ProductFilter.Matches(storage, "nvme 256 adata"));
        Assert.True(ProductFilter.Matches(storage, "adata nvme 256"));
        Assert.False(ProductFilter.Matches(storage, "nvme 512 adata"));
    }

    [Fact]
    public void NegativeTerms_AreNotHighlighted()
    {
        const string text = "AMD Ryzen sem notebook";

        var ranges = ProductFilter.GetHighlightRanges(text, "Ryzen -note*");
        var highlighted = ranges
            .Select(range => text.Substring(range.Start, range.Length))
            .ToList();

        Assert.Equal(["Ryzen"], highlighted);
    }
}
