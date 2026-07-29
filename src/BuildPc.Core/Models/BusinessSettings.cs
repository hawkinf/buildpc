namespace BuildPc.Core.Models;

public sealed record BusinessSettings
{
    public const decimal MinimumMarginPercent = 15m;

    public decimal GlobalMarginPercent { get; init; } = MinimumMarginPercent;
    public Dictionary<ComponentCategory, decimal> CategoryMargins { get; init; } = [];
    public List<ProductCategoryDefinition>? ProductCategories { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string CompanyDocument { get; init; } = string.Empty;
    public string CompanyPhone { get; init; } = string.Empty;
    public string CompanyEmail { get; init; } = string.Empty;
    public string CompanyWebsite { get; init; } = string.Empty;
    public string CompanyAddress { get; init; } = string.Empty;
    public string LogoPath { get; init; } = string.Empty;
    public string AdditionalQuoteInfo { get; init; } = string.Empty;
    public AppThemeMode ThemeMode { get; init; } = AppThemeMode.System;

    public decimal MarginFor(ComponentCategory category)
    {
        var configuredMargin = CategoryMargins.TryGetValue(category, out var margin)
            ? margin
            : GlobalMarginPercent;
        return Math.Max(MinimumMarginPercent, configuredMargin);
    }

    public IReadOnlyList<ProductCategoryDefinition> EffectiveProductCategories()
    {
        if (ProductCategories is null or { Count: 0 })
        {
            return ProductCategoryDefinition.Defaults();
        }

        var categories = ProductCategories
            .Where(category => !string.IsNullOrWhiteSpace(category.Name))
            .GroupBy(category => category.Value)
            .Select(group => group.Last() with
            {
                Name = group.Last().Name.Trim()
            })
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return categories.Count == 0
            ? ProductCategoryDefinition.Defaults()
            : categories;
    }
}
