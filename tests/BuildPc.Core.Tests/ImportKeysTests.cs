using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

public sealed class ImportKeysTests
{
    [Fact]
    public void SourceUrlKey_CombinesSourceAndCategoryVerbatim()
    {
        Assert.Equal(
            "kabum:Processor",
            ImportKeys.SourceUrlKey(ComponentCategory.Processor, "kabum"));
        Assert.Equal(
            "kabum-hd:HardDrive",
            ImportKeys.SourceUrlKey(ComponentCategory.HardDrive, "kabum-hd"));
    }

    [Fact]
    public void SourceUrlKey_DiffersFromTheLastImportMetadataKeyFormat()
    {
        // For() usa a origem em minúsculas e a categoria como número; já
        // SourceUrlKey() preserva a origem como veio e usa o nome do enum —
        // são chaves de finalidades diferentes, não intercambiáveis.
        var metadataKey = ImportKeys.For(ComponentCategory.Processor, "Kabum");
        var urlKey = ImportKeys.SourceUrlKey(ComponentCategory.Processor, "Kabum");

        Assert.NotEqual(metadataKey, urlKey);
        Assert.Equal("kabum:0", metadataKey);
        Assert.Equal("Kabum:Processor", urlKey);
    }
}
