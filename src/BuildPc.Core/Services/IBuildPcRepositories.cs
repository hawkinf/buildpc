using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

public interface IComponentCatalogRepository
{
    IReadOnlyList<PcComponent> GetAll();
    void Add(PcComponent component);
    bool Update(PcComponent component);
    bool Delete(string componentId);
    int DeleteMany(IEnumerable<string> componentIds);
    int UpdateDescriptions(
        IEnumerable<string> componentIds,
        string description,
        BulkDescriptionMode mode);
    ImportReplaceResult ReplaceImported(
        ComponentCategory category,
        string source,
        IEnumerable<PcComponent> components);
    DateTimeOffset? GetLastImport(ComponentCategory category, string source);

    /// <summary>
    /// Devolve a data da última importação de todas as categorias de uma vez,
    /// indexada por <see cref="ImportKeys.For"/>.
    /// </summary>
    /// <remarks>
    /// A tela de Importações mostra doze cartões. Consultar um a um custava doze
    /// idas ao servidor durante a abertura do programa.
    /// </remarks>
    IReadOnlyDictionary<string, DateTimeOffset> GetLastImports();
    bool SetKeepOnImport(string componentId, bool keep);
}

public interface IQuoteRepository
{
    BusinessSettings GetSettings();
    void SaveSettings(BusinessSettings settings);
    SavedQuote SaveQuote(
        SavedQuote? existing,
        string clientName,
        string clientPhone,
        string notes,
        IReadOnlyList<SavedQuoteItem> items,
        BusinessSettings companySnapshot);
    IReadOnlyList<SavedQuote> GetQuotes();

    /// <summary>
    /// Remove um orçamento gravado. Devolve <c>false</c> quando o orçamento já
    /// não existe. O número não é reaproveitado.
    /// </summary>
    bool DeleteQuote(Guid quoteId);
}
