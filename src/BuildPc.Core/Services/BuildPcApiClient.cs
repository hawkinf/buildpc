using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

/// <summary>
/// Acesso ao catálogo e aos orçamentos por meio da API privada.
/// </summary>
/// <remarks>
/// Todas as chamadas são assíncronas de verdade. A versão anterior usava
/// <c>HttpClient.Send</c> e <c>GetAwaiter().GetResult()</c>, o que bloqueava a
/// thread da interface até o tempo limite de 60 segundos em cada operação.
/// </remarks>
public sealed class BuildPcApiClient :
    IComponentCatalogRepository,
    IQuoteRepository,
    IAssemblyTemplateRepository,
    IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public BuildPcApiClient(BuildPcApiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsValid())
        {
            throw new ArgumentException(
                "A configuração da API do BuildPC é inválida.",
                nameof(settings));
        }

        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
        {
            BaseAddress = new Uri(EnsureTrailingSlash(settings.BaseUrl)),
            Timeout = TimeSpan.FromSeconds(60)
        };
        _httpClient.DefaultRequestHeaders.Add("X-BuildPc-Key", settings.ApiKey);
    }

    internal BuildPcApiClient(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("X-BuildPc-Key", apiKey);
    }

    public async Task<IReadOnlyList<PcComponent>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        await SendAsync<List<PcComponent>>(
            HttpMethod.Get,
            "products",
            cancellationToken: cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(
        PcComponent component,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(component);
        await SendAsync(
            HttpMethod.Post,
            "products",
            component,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> UpdateAsync(
        PcComponent component,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(component);
        var response = await SendAsync<BooleanResponse>(
            HttpMethod.Put,
            $"products/{Escape(component.Id)}",
            component,
            cancellationToken).ConfigureAwait(false);
        return response.Value;
    }

    public async Task<bool> DeleteAsync(
        string componentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        var response = await SendAsync<BooleanResponse>(
            HttpMethod.Delete,
            $"products/{Escape(componentId)}",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Value;
    }

    public async Task<int> DeleteManyAsync(
        IEnumerable<string> componentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(componentIds);
        var response = await SendAsync<AffectedRowsResponse>(
            HttpMethod.Post,
            "products/delete",
            new DeleteProductsRequest(componentIds.ToList()),
            cancellationToken).ConfigureAwait(false);
        return response.Count;
    }

    public async Task<int> UpdateDescriptionsAsync(
        IEnumerable<string> componentIds,
        string description,
        BulkDescriptionMode mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(componentIds);
        var response = await SendAsync<AffectedRowsResponse>(
            HttpMethod.Post,
            "products/descriptions",
            new UpdateDescriptionsRequest(componentIds.ToList(), description, mode),
            cancellationToken).ConfigureAwait(false);
        return response.Count;
    }

    public async Task<ImportReplaceResult> ReplaceImportedAsync(
        ComponentCategory category,
        string source,
        IEnumerable<PcComponent> components,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(components);
        return await SendAsync<ImportReplaceResult>(
            HttpMethod.Post,
            "imports/replace",
            new ReplaceImportedProductsRequest(category, source, components.ToList()),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, DateTimeOffset>> GetLastImportsAsync(
        CancellationToken cancellationToken = default) =>
        await SendAsync<Dictionary<string, DateTimeOffset>>(
            HttpMethod.Get,
            "imports/last-all",
            cancellationToken: cancellationToken).ConfigureAwait(false);

    public async Task<bool> SetKeepOnImportAsync(
        string componentId,
        bool keep,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<BooleanResponse>(
            HttpMethod.Put,
            $"products/{Escape(componentId)}/keep",
            new SetKeepOnImportRequest(keep),
            cancellationToken).ConfigureAwait(false);
        return response.Value;
    }

    public async Task<bool> SetFavoriteAsync(
        string componentId,
        bool favorite,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<BooleanResponse>(
            HttpMethod.Put,
            $"products/{Escape(componentId)}/favorite",
            new SetFavoriteRequest(favorite),
            cancellationToken).ConfigureAwait(false);
        return response.Value;
    }

    public async Task<IReadOnlyList<PriceHistoryEntry>> GetPriceHistoryAsync(
        string componentId,
        CancellationToken cancellationToken = default) =>
        await SendAsync<List<PriceHistoryEntry>>(
            HttpMethod.Get,
            $"products/{Escape(componentId)}/price-history",
            cancellationToken: cancellationToken).ConfigureAwait(false);

    public async Task<BusinessSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default) =>
        await SendAsync<BusinessSettings>(
            HttpMethod.Get,
            "settings",
            cancellationToken: cancellationToken).ConfigureAwait(false);

    public async Task SaveSettingsAsync(
        BusinessSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await SendAsync(
            HttpMethod.Put,
            "settings",
            settings,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetImportSourceUrlsAsync(
        CancellationToken cancellationToken = default) =>
        await SendAsync<Dictionary<string, string>>(
            HttpMethod.Get,
            "settings/import-sources",
            cancellationToken: cancellationToken).ConfigureAwait(false);

    public async Task SaveImportSourceUrlsAsync(
        IReadOnlyDictionary<string, string> importSourceUrls,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(importSourceUrls);
        await SendAsync(
            HttpMethod.Put,
            "settings/import-sources",
            importSourceUrls,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<SavedQuote> SaveQuoteAsync(
        SavedQuote? existing,
        QuoteDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return await SendAsync<SavedQuote>(
            HttpMethod.Post,
            "quotes",
            new SaveQuoteRequest(existing, draft),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SavedQuote>> GetQuotesAsync(
        CancellationToken cancellationToken = default) =>
        await SendAsync<List<SavedQuote>>(
            HttpMethod.Get,
            "quotes",
            cancellationToken: cancellationToken).ConfigureAwait(false);

    public async Task<bool> DeleteQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<BooleanResponse>(
            HttpMethod.Delete,
            $"quotes/{Escape(quoteId.ToString("D"))}",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Value;
    }

    public async Task<IReadOnlyList<AssemblyTemplate>> GetTemplatesAsync(
        CancellationToken cancellationToken = default) =>
        await SendAsync<List<AssemblyTemplate>>(
            HttpMethod.Get,
            "templates",
            cancellationToken: cancellationToken).ConfigureAwait(false);

    public async Task<AssemblyTemplate> SaveTemplateAsync(
        AssemblyTemplate template,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        return await SendAsync<AssemblyTemplate>(
            HttpMethod.Post,
            "templates",
            template,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<BooleanResponse>(
            HttpMethod.Delete,
            $"templates/{Escape(templateId.ToString("D"))}",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Value;
    }

    public async Task TestConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient
            .GetAsync("connection", cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            throw CreateHttpError(response, body);
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var document = await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("status", out var status) ||
            !string.Equals(
                status.GetString(),
                "ok",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "O endereço respondeu, mas não parece ser uma API do BuildPC.");
        }
    }

    public void Dispose() => _httpClient.Dispose();

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? content = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendRequestAsync(
            method,
            path,
            content,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await DeserializeAsync<T>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SendAsync(
        HttpMethod method,
        string path,
        object? content = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendRequestAsync(
            method,
            path,
            content,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        HttpMethod method,
        string path,
        object? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (content is not null)
        {
            request.Content = JsonContent.Create(content, options: JsonOptions);
        }

        try
        {
            return await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                "Não foi possível acessar o servidor do BuildPC.",
                exception);
        }
        catch (TaskCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "O servidor do BuildPC demorou demais para responder.",
                exception);
        }
    }

    private static async Task<T> DeserializeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonSerializer
                   .DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
                   .ConfigureAwait(false) ??
               throw new InvalidOperationException(
                   "O servidor do BuildPC devolveu uma resposta vazia.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
        throw CreateHttpError(response, body);
    }

    private static InvalidOperationException CreateHttpError(
        HttpResponseMessage response,
        string body)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return new InvalidOperationException(
                "O servidor recusou por excesso de requisições. " +
                "Aguarde um instante e tente novamente.");
        }

        var detail = TryReadProblemDetail(body);
        return new InvalidOperationException(
            detail ??
            $"O servidor do BuildPC respondeu com o código {(int)response.StatusCode}.");
    }

    private static string? TryReadProblemDetail(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.GetString();
            }

            if (document.RootElement.TryGetProperty("title", out var title))
            {
                return title.GetString();
            }
        }
        catch (JsonException)
        {
            // O corpo sem JSON não deve esconder o código HTTP.
        }

        return string.IsNullOrWhiteSpace(body) ? null : body.Trim();
    }

    private static string Escape(string value) =>
        Uri.EscapeDataString(value);

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith('/') ? value : $"{value}/";
}
