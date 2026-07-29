using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

/// <summary>Períodos pré-definidos para filtrar a lista de orçamentos.</summary>
public enum QuotePeriod
{
    All,
    Last7Days,
    Last30Days,
    Last90Days,
    ThisYear
}

/// <summary>
/// Busca e recorte por período na lista de orçamentos gravados.
/// </summary>
/// <remarks>
/// A lista só crescia e não tinha filtro nenhum. A busca cobre número, cliente,
/// telefone e os títulos dos itens, porque é por qualquer um desses que se
/// procura um orçamento antigo.
/// </remarks>
public static class QuoteFilter
{
    public static bool Matches(SavedQuote quote, string? search)
    {
        ArgumentNullException.ThrowIfNull(quote);
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        // Cada palavra precisa aparecer em algum campo, como no filtro de
        // produtos, para "maria placa" encontrar o orçamento da Maria com placa.
        var terms = search.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var searchable = BuildSearchableText(quote);
        return terms.All(term =>
            searchable.Contains(term, StringComparison.CurrentCultureIgnoreCase));
    }

    public static string BuildSearchableText(SavedQuote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        return string.Join(
            " ",
            quote.Number.ToString("000000"),
            quote.Number.ToString(),
            quote.ClientName,
            quote.ClientPhone,
            // O telefone é procurado tanto com máscara quanto sem.
            new string([.. quote.ClientPhone.Where(char.IsDigit)]),
            quote.Notes,
            string.Join(" ", quote.Items.Select(item => item.Name)),
            string.Join(" ", quote.Items.Select(item => item.CategoryName)));
    }

    public static bool IsInPeriod(
        SavedQuote quote,
        QuotePeriod period,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(quote);
        return period switch
        {
            QuotePeriod.Last7Days => quote.CreatedAt >= now.AddDays(-7),
            QuotePeriod.Last30Days => quote.CreatedAt >= now.AddDays(-30),
            QuotePeriod.Last90Days => quote.CreatedAt >= now.AddDays(-90),
            QuotePeriod.ThisYear => quote.CreatedAt.Year == now.Year,
            _ => true
        };
    }

    public static string DisplayName(QuotePeriod period) => period switch
    {
        QuotePeriod.Last7Days => "Últimos 7 dias",
        QuotePeriod.Last30Days => "Últimos 30 dias",
        QuotePeriod.Last90Days => "Últimos 90 dias",
        QuotePeriod.ThisYear => "Este ano",
        _ => "Todo o período"
    };
}
