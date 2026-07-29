using BuildPc.Core.Models;

namespace BuildPc.Api.Database;

public sealed record BuildPcDatabaseSnapshot(
    IReadOnlyList<PcComponent> Products,
    BusinessSettings Settings,
    IReadOnlyList<SavedQuote> Quotes,
    IReadOnlyDictionary<string, string> Metadata,
    /// <summary>
    /// Modelos de montagem. São dados do usuário: sem eles na migração, as
    /// combinações salvas desapareceriam ao passar para o PostgreSQL.
    /// </summary>
    IReadOnlyList<AssemblyTemplate> Templates)
{
    /// <summary>Sobrecarga para snapshots gravados antes dos modelos existirem.</summary>
    public BuildPcDatabaseSnapshot(
        IReadOnlyList<PcComponent> products,
        BusinessSettings settings,
        IReadOnlyList<SavedQuote> quotes,
        IReadOnlyDictionary<string, string> metadata)
        : this(products, settings, quotes, metadata, [])
    {
    }
}
