namespace BuildPc.Desktop.Services;

public static class ImportProgressCalculator
{
    public static double Calculate(
        int position,
        int total,
        double currentCategoryProgress)
    {
        var safeTotal = Math.Max(1, total);
        var completedCategories = Math.Max(0, position - 1);
        return Math.Clamp(
            (completedCategories +
             Math.Clamp(currentCategoryProgress, 0d, 1d)) /
            safeTotal *
            100d,
            0d,
            100d);
    }
}
