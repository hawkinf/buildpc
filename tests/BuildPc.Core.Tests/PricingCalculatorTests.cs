using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

public sealed class PricingCalculatorTests
{
    [Theory]
    // Exemplos exatos do pedido do usuário.
    [InlineData(81.20, 84.90)]
    [InlineData(79.91, 84.90)]
    [InlineData(84.90, 84.90)]
    [InlineData(84.91, 89.90)]
    // Limites das primeiras faixas.
    [InlineData(0.01, 4.90)]
    [InlineData(4.90, 4.90)]
    [InlineData(4.91, 9.90)]
    [InlineData(9.90, 9.90)]
    [InlineData(9.91, 14.90)]
    public void RoundUpToNinetyCents_RoundsUpToTheNextFiveReaisStep(
        decimal value,
        decimal expected)
    {
        Assert.Equal(expected, PricingCalculator.RoundUpToNinetyCents(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void RoundUpToNinetyCents_ZeroOrNegativeReturnsTheSmallestStep(decimal value)
    {
        Assert.Equal(4.90m, PricingCalculator.RoundUpToNinetyCents(value));
    }

    [Fact]
    public void CalculateSalePrice_AppliesTheMarginThenRoundsToTheFiveReaisStep()
    {
        // custo 100, margem 20% => 120,00 => arredonda pra 124,90.
        Assert.Equal(124.90m, PricingCalculator.CalculateSalePrice(100m, 20m));
    }
}
