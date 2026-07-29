namespace BuildPc.Core.Models;

/// <summary>
/// Um item de um modelo de montagem: referência a um produto do catálogo e a
/// quantidade sugerida.
/// </summary>
/// <remarks>
/// O modelo guarda apenas o identificador, não o preço. Ao aplicar, os valores
/// vêm do catálogo atual — é isso que distingue um modelo de um orçamento
/// gravado, que congela os valores acordados.
/// </remarks>
public sealed record AssemblyTemplateItem
{
    public string ComponentId { get; init; } = string.Empty;
    public ComponentCategory Category { get; init; }
    public int Quantity { get; init; } = 1;
}

/// <summary>
/// Combinação de produtos reutilizável, como “PC Gamer básico” ou “Office”.
/// </summary>
public sealed record AssemblyTemplate
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public IReadOnlyList<AssemblyTemplateItem> Items { get; init; } = [];

    public int TotalItems => Items.Sum(item => item.Quantity);
}
