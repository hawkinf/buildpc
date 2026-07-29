using System.Text.RegularExpressions;
using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

public static class ProductFilter
{
    public static bool Matches(PcComponent component, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        var searchableText = string.Join(
            " ",
            component.Name,
            component.Brand,
            component.Description,
            component.Socket,
            component.MemoryType,
            component.FormFactor,
            component.Category,
            string.Join(" ", component.SupportedSockets),
            string.Join(" ", component.SupportedFormFactors));
        var (positiveTerms, excludedTerms) = ParseTerms(filter);

        if (positiveTerms.Any(term => !MatchesText(searchableText, term)))
        {
            return false;
        }

        return excludedTerms.All(term => !MatchesText(searchableText, term));
    }

    public static IReadOnlyList<(int Start, int Length)> GetHighlightRanges(
        string? text,
        string? filter)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(filter))
        {
            return [];
        }

        var (terms, _) = ParseTerms(filter);
        if (terms.Count == 0)
        {
            return [];
        }

        var fragments = terms.SelectMany(term =>
            term.Contains('*') || term.Contains('?')
                ? term.Split(
                    ['*', '?'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [term]);

        var ranges = new List<(int Start, int Length)>();
        foreach (var fragment in fragments.Where(fragment => fragment.Length > 0))
        {
            var searchFrom = 0;
            while (searchFrom < text.Length)
            {
                var index = text.IndexOf(
                    fragment,
                    searchFrom,
                    StringComparison.CurrentCultureIgnoreCase);
                if (index < 0)
                {
                    break;
                }

                ranges.Add((index, fragment.Length));
                searchFrom = index + fragment.Length;
            }
        }

        if (ranges.Count < 2)
        {
            return ranges;
        }

        var ordered = ranges.OrderBy(range => range.Start).ToList();
        var merged = new List<(int Start, int Length)> { ordered[0] };
        foreach (var range in ordered.Skip(1))
        {
            var previous = merged[^1];
            var previousEnd = previous.Start + previous.Length;
            if (range.Start <= previousEnd)
            {
                var combinedEnd = Math.Max(previousEnd, range.Start + range.Length);
                merged[^1] = (previous.Start, combinedEnd - previous.Start);
            }
            else
            {
                merged.Add(range);
            }
        }

        return merged;
    }

    private static bool MatchesText(string text, string term)
    {
        if (!term.Contains('*') && !term.Contains('?'))
        {
            return text.Contains(term, StringComparison.CurrentCultureIgnoreCase);
        }

        var regexPattern = Regex.Escape(term)
            .Replace(@"\*", ".*", StringComparison.Ordinal)
            .Replace(@"\?", ".", StringComparison.Ordinal);

        return Regex.IsMatch(
            text,
            regexPattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline,
            TimeSpan.FromMilliseconds(100));
    }

    private static (
        IReadOnlyList<string> PositiveTerms,
        IReadOnlyList<string> ExcludedTerms) ParseTerms(
        string filter)
    {
        var positiveTerms = new List<string>();
        var excludedTerms = new List<string>();

        foreach (var term in filter.Split(
                     (char[]?)null,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (term.Length > 1 && term[0] == '-')
            {
                excludedTerms.Add(term[1..]);
            }
            else
            {
                positiveTerms.Add(term);
            }
        }

        return (positiveTerms, excludedTerms);
    }
}
