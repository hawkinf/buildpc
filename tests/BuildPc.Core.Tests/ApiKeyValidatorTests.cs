using BuildPc.Api.Security;
using Microsoft.Extensions.Configuration;

namespace BuildPc.Core.Tests;

public sealed class ApiKeyValidatorTests
{
    [Fact]
    public void AcceptsTheCurrentKeyAndRejectsAnythingElse()
    {
        var validator = new ApiKeyValidator(["chave-atual"]);

        Assert.True(validator.IsValid("chave-atual"));
        Assert.False(validator.IsValid("chave-errada"));
        Assert.False(validator.IsValid(string.Empty));
        Assert.False(validator.IsValid(null));
        Assert.Equal(1, validator.AcceptedKeyCount);
    }

    [Fact]
    public void DuringRotationBothKeysAreAccepted()
    {
        var validator = new ApiKeyValidator(["chave-nova", "chave-anterior"]);

        // É isso que permite trocar a chave sem atualizar servidor e clientes
        // no mesmo instante.
        Assert.True(validator.IsValid("chave-nova"));
        Assert.True(validator.IsValid("chave-anterior"));
        Assert.False(validator.IsValid("chave-antiga-demais"));
        Assert.Equal(2, validator.AcceptedKeyCount);
    }

    [Fact]
    public void BlankAndRepeatedKeysAreIgnored()
    {
        var validator = new ApiKeyValidator(
            ["chave", "   ", string.Empty, "chave"]);

        Assert.Equal(1, validator.AcceptedKeyCount);
        Assert.True(validator.IsValid("chave"));
    }

    [Fact]
    public void ConstructionFailsWithoutAnyUsableKey()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ApiKeyValidator([string.Empty, "  "]));
    }

    [Fact]
    public void ComparisonIsCaseAndWhitespaceSensitive()
    {
        var validator = new ApiKeyValidator(["ChaveSecreta"]);

        Assert.True(validator.IsValid("ChaveSecreta"));
        Assert.False(validator.IsValid("chavesecreta"));
        Assert.False(validator.IsValid(" ChaveSecreta"));
    }

    [Fact]
    public void FromConfigurationReadsCurrentAndPreviousKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BuildPc:ApiKey"] = "atual",
                ["BuildPc:PreviousApiKey"] = "anterior"
            })
            .Build();

        var validator = ApiKeyValidator.FromConfiguration(configuration);

        Assert.Equal(2, validator.AcceptedKeyCount);
        Assert.True(validator.IsValid("atual"));
        Assert.True(validator.IsValid("anterior"));
    }

    [Fact]
    public void FromConfigurationWorksWithoutAPreviousKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BuildPc:ApiKey"] = "atual"
            })
            .Build();

        var validator = ApiKeyValidator.FromConfiguration(configuration);

        Assert.Equal(1, validator.AcceptedKeyCount);
        Assert.True(validator.IsValid("atual"));
    }

    [Fact]
    public void FromConfigurationRequiresTheCurrentKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BuildPc:PreviousApiKey"] = "anterior"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            ApiKeyValidator.FromConfiguration(configuration));
    }
}
