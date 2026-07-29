using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

public sealed record DeleteProductsRequest(IReadOnlyList<string> Ids);

public sealed record UpdateDescriptionsRequest(
    IReadOnlyList<string> Ids,
    string Description,
    BulkDescriptionMode Mode);

public sealed record ReplaceImportedProductsRequest(
    ComponentCategory Category,
    string Source,
    IReadOnlyList<PcComponent> Components);

public sealed record SetKeepOnImportRequest(bool Keep);

public sealed record SetFavoriteRequest(bool Favorite);

public sealed record SaveQuoteRequest(
    SavedQuote? Existing,
    QuoteDraft Draft);

public sealed record AffectedRowsResponse(int Count);

public sealed record BooleanResponse(bool Value);
