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

    /// <summary>Soma dos itens, antes do desconto.</summary>
    public decimal TotalPrice { get; init; }

    /// <summary>
    /// Desconto concedido, em reais. Orçamentos gravados antes deste campo
    /// existir são lidos com zero e continuam válidos.
    /// </summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>Dias de validade da proposta. Zero significa sem prazo.</summary>
    public int ValidityDays { get; init; }

    public string PaymentTerms { get; init; } = string.Empty;
    public string DeliveryTerms { get; init; } = string.Empty;

    public IReadOnlyList<SavedQuoteItem> Items { get; init; } = [];
    public CompanySnapshot CompanySnapshot { get; init; } = new();

    /// <summary>Valor que o cliente paga: total dos itens menos o desconto.</summary>
    public decimal FinalPrice => Math.Max(0m, TotalPrice - DiscountAmount);

    public decimal TotalProfit => FinalPrice - TotalCost;

    public bool HasDiscount => DiscountAmount > 0m;

    /// <summary>Último dia de validade, ou <c>null</c> quando não há prazo.</summary>
    public DateTimeOffset? ValidUntil => ValidityDays > 0
        ? CreatedAt.AddDays(ValidityDays)
        : null;
}
