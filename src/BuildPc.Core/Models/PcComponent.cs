namespace BuildPc.Core.Models;

public sealed record PcComponent
{
    public required string Id { get; init; }
    public required ComponentCategory Category { get; init; }
    public required string Name { get; init; }
    public required string Brand { get; init; }
    public required string Description { get; init; }
    public required decimal Price { get; init; }
    public string? ImageUrl { get; init; }
    public int PowerWatts { get; init; }
    public string? Socket { get; init; }
    public string? MemoryType { get; init; }
    public string? FormFactor { get; init; }
    public HashSet<string> SupportedSockets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> SupportedFormFactors { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? ImportSource { get; init; }
    public bool KeepOnImport { get; init; }
    public bool IsUserDefined { get; init; }

    /// <summary>
    /// Produto marcado como favorito, exibido antes dos demais nas listas de
    /// seleção. Preservado nas importações, como <see cref="KeepOnImport"/>.
    /// </summary>
    public bool IsFavorite { get; init; }
}
