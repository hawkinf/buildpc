using BuildPc.Desktop.Services;

namespace BuildPc.Core.Tests;

public sealed class ImportProgressCalculatorTests
{
    [Fact]
    public void Calculate_CombinesCompletedCategoriesAndCurrentStage()
    {
        Assert.Equal(
            4.1667d,
            ImportProgressCalculator.Calculate(1, 12, 0.5d),
            precision: 4);
        Assert.Equal(
            50d,
            ImportProgressCalculator.Calculate(6, 12, 1d));
        Assert.Equal(
            100d,
            ImportProgressCalculator.Calculate(1, 1, 1d));
        Assert.Equal(
            0d,
            ImportProgressCalculator.Calculate(1, 0, -1d));
    }
}
