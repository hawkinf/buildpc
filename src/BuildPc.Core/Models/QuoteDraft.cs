namespace BuildPc.Core.Models;

/// <summary>
/// Dados de um orçamento a gravar.
/// </summary>
/// <remarks>
/// Substitui a lista de seis parâmetros posicionais que <c>SaveQuote</c> recebia.
/// Com desconto, validade e condições de pagamento a assinatura passaria de dez
/// argumentos, onde é fácil trocar dois textos de lugar sem o compilador notar.
/// </remarks>
public sealed record QuoteDraft
{
    public string ClientName { get; init; } = string.Empty;
    public string ClientPhone { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public IReadOnlyList<SavedQuoteItem> Items { get; init; } = [];
    public BusinessSettings CompanySnapshot { get; init; } = new();

    /// <summary>Desconto concedido sobre o total de venda, em reais.</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>Dias de validade da proposta. Zero significa sem prazo.</summary>
    public int ValidityDays { get; init; }

    public string PaymentTerms { get; init; } = string.Empty;
    public string DeliveryTerms { get; init; } = string.Empty;
}
