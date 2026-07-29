namespace BuildPc.Core.Models;

/// <summary>
/// Custo que um produto tinha em um momento anterior.
/// </summary>
/// <remarks>
/// Registrado a cada importação quando o preço muda, para mostrar a variação
/// desde a carga anterior.
/// </remarks>
public sealed record PriceHistoryEntry
{
    public string ComponentId { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public DateTimeOffset RecordedAt { get; init; }

    /// <summary>Origem da mudança: <c>kabum</c>, <c>manual</c> e afins.</summary>
    public string Source { get; init; } = string.Empty;
}

/// <summary>
/// Variação de preço de um produto entre a carga anterior e a atual.
/// </summary>
public sealed record PriceChange
{
    public string ComponentId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal PreviousPrice { get; init; }
    public decimal CurrentPrice { get; init; }

    public decimal Difference => CurrentPrice - PreviousPrice;

    public decimal PercentChange => PreviousPrice <= 0
        ? 0
        : decimal.Round(
            (CurrentPrice / PreviousPrice - 1m) * 100m,
            2,
            MidpointRounding.AwayFromZero);

    public bool IsIncrease => CurrentPrice > PreviousPrice;
}
