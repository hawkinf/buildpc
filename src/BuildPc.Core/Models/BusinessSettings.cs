namespace BuildPc.Core.Models;

public sealed record BusinessSettings
{
    public const decimal MinimumMarginPercent = 15m;

    public decimal GlobalMarginPercent { get; init; } = MinimumMarginPercent;
    public Dictionary<ComponentCategory, decimal> CategoryMargins { get; init; } = [];
    public string CompanyName { get; init; } = string.Empty;
    public string CompanyDocument { get; init; } = string.Empty;
    public string CompanyPhone { get; init; } = string.Empty;
    public string CompanyEmail { get; init; } = string.Empty;
    public string CompanyWebsite { get; init; } = string.Empty;
    public string CompanyAddress { get; init; } = string.Empty;
    public string LogoPath { get; init; } = string.Empty;
    public string AdditionalQuoteInfo { get; init; } = string.Empty;

    public decimal MarginFor(ComponentCategory category)
    {
        var configuredMargin = CategoryMargins.TryGetValue(category, out var margin)
            ? margin
            : GlobalMarginPercent;
        return Math.Max(MinimumMarginPercent, configuredMargin);
    }
}
