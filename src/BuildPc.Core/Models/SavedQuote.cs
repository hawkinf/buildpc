namespace BuildPc.Core.Models;

public sealed record SavedQuoteItem
{
    public string ComponentId { get; init; } = string.Empty;
    public ComponentCategory Category { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public decimal MarginPercent { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal TotalPrice => UnitPrice * Quantity;
}

public sealed record SavedQuote
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string ClientPhone { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public decimal TotalCost { get; init; }
    public decimal TotalPrice { get; init; }
    public decimal TotalProfit => TotalPrice - TotalCost;
    public IReadOnlyList<SavedQuoteItem> Items { get; init; } = [];
    public BusinessSettings CompanySnapshot { get; init; } = new();
}
