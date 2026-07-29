using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

/// <summary>
/// Catálogo de produtos.
/// </summary>
/// <remarks>
/// Todas as operações são assíncronas porque a implementação sobre a API faz
/// chamadas de rede: a versão síncrona anterior bloqueava a interface até o
/// tempo limite de 60 segundos em cada leitura ou gravação. A implementação
/// SQLite é local e rápida, e apenas devolve tarefas já concluídas.
/// </remarks>
public interface IComponentCatalogRepository
{
    Task<IReadOnlyList<PcComponent>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(
        PcComponent component,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        PcComponent component,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string componentId,
        CancellationToken cancellationToken = default);

    Task<int> DeleteManyAsync(
        IEnumerable<string> componentIds,
        CancellationToken cancellationToken = default);

    Task<int> UpdateDescriptionsAsync(
        IEnumerable<string> componentIds,
        string description,
        BulkDescriptionMode mode,
        CancellationToken cancellationToken = default);

    Task<ImportReplaceResult> ReplaceImportedAsync(
        ComponentCategory category,
        string source,
        IEnumerable<PcComponent> components,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devolve a data da última importação de todas as categorias de uma vez,
    /// indexada por <see cref="ImportKeys.For"/>.
    /// </summary>
    Task<IReadOnlyDictionary<string, DateTimeOffset>> GetLastImportsAsync(
        CancellationToken cancellationToken = default);

    Task<bool> SetKeepOnImportAsync(
        string componentId,
        bool keep,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca ou desmarca um produto como favorito, para aparecer no topo das
    /// listas de seleção.
    /// </summary>
    Task<bool> SetFavoriteAsync(
        string componentId,
        bool favorite,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Preços anteriores de um produto, do mais recente para o mais antigo.
    /// </summary>
    Task<IReadOnlyList<PriceHistoryEntry>> GetPriceHistoryAsync(
        string componentId,
        CancellationToken cancellationToken = default);
}

public interface IQuoteRepository
{
    Task<BusinessSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(
        BusinessSettings settings,
        CancellationToken cancellationToken = default);

    Task<SavedQuote> SaveQuoteAsync(
        SavedQuote? existing,
        QuoteDraft draft,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedQuote>> GetQuotesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove um orçamento gravado. Devolve <c>false</c> quando o orçamento já
    /// não existe. O número não é reaproveitado.
    /// </summary>
    Task<bool> DeleteQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Modelos de montagem reutilizáveis, para não remontar de novo as combinações
/// mais vendidas.
/// </summary>
public interface IAssemblyTemplateRepository
{
    Task<IReadOnlyList<AssemblyTemplate>> GetTemplatesAsync(
        CancellationToken cancellationToken = default);

    Task<AssemblyTemplate> SaveTemplateAsync(
        AssemblyTemplate template,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken = default);
}
