using BuildPc.Core.Services;
using Microsoft.Extensions.Configuration;

namespace BuildPc.Core.Tests;

public sealed class StaffPasswordValidatorTests
{
    [Fact]
    public void IsValid_AcceptsTheConfiguredPassword()
    {
        var validator = new StaffPasswordValidator("senha-correta");

        Assert.True(validator.IsValid("senha-correta"));
    }

    [Theory]
    [InlineData("senha-errada")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_RejectsAnythingElse(string? suppliedPassword)
    {
        var validator = new StaffPasswordValidator("senha-correta");

        Assert.False(validator.IsValid(suppliedPassword));
    }

    [Fact]
    public void Constructor_RejectsAnEmptyConfiguredPassword()
    {
        Assert.Throws<ArgumentException>(() => new StaffPasswordValidator(" "));
    }

    [Fact]
    public void FromConfiguration_ReadsTheSharedPasswordKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BuildPc:WebPassword"] = "senha-da-equipe"
            })
            .Build();

        var validator = StaffPasswordValidator.FromConfiguration(configuration);

        Assert.True(validator.IsValid("senha-da-equipe"));
    }

    [Fact]
    public void FromConfiguration_ThrowsWhenTheSharedPasswordIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(
            () => StaffPasswordValidator.FromConfiguration(configuration));
    }
}
