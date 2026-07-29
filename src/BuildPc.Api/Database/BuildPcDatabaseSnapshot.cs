using BuildPc.Core.Models;

namespace BuildPc.Api.Database;

public sealed record BuildPcDatabaseSnapshot(
    IReadOnlyList<PcComponent> Products,
    BusinessSettings Settings,
    IReadOnlyList<SavedQuote> Quotes,
    IReadOnlyDictionary<string, string> Metadata);
