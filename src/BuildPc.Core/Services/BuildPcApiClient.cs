using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

public sealed class BuildPcApiClient :
    IComponentCatalogRepository,
    IQuoteRepository,
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

        _httpClient = new HttpClient
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

    public IReadOnlyList<PcComponent> GetAll() =>
        Send<List<PcComponent>>(HttpMethod.Get, "products");

    public void Add(PcComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        Send(HttpMethod.Post, "products", component);
    }

    public bool Update(PcComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        return Send<BooleanResponse>(
            HttpMethod.Put,
            $"products/{Escape(component.Id)}",
            component).Value;
    }

    public bool Delete(string componentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        return Send<BooleanResponse>(
            HttpMethod.Delete,
            $"products/{Escape(componentId)}").Value;
    }

    public int DeleteMany(IEnumerable<string> componentIds)
    {
        ArgumentNullException.ThrowIfNull(componentIds);
        return Send<AffectedRowsResponse>(
            HttpMethod.Post,
            "products/delete",
            new DeleteProductsRequest(componentIds.ToList())).Count;
    }

    public int UpdateDescriptions(
        IEnumerable<string> componentIds,
        string description,
        BulkDescriptionMode mode)
    {
        ArgumentNullException.ThrowIfNull(componentIds);
        return Send<AffectedRowsResponse>(
            HttpMethod.Post,
            "products/descriptions",
            new UpdateDescriptionsRequest(
                componentIds.ToList(),
                description,
                mode)).Count;
    }

    public ImportReplaceResult ReplaceImported(
        ComponentCategory category,
        string source,
        IEnumerable<PcComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        return Send<ImportReplaceResult>(
            HttpMethod.Post,
            "imports/replace",
            new ReplaceImportedProductsRequest(
                category,
                source,
                components.ToList()));
    }

    public DateTimeOffset? GetLastImport(ComponentCategory category, string source)
    {
        var path =
            $"imports/last?category={(int)category}&source={Uri.EscapeDataString(source)}";
        using var response = SendRequest(HttpMethod.Get, path);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        EnsureSuccess(response);
        return Deserialize<DateTimeOffset>(response);
    }

    public IReadOnlyDictionary<string, DateTimeOffset> GetLastImports() =>
        Send<Dictionary<string, DateTimeOffset>>(
            HttpMethod.Get,
            "imports/last-all");

    public bool SetKeepOnImport(string componentId, bool keep) =>
        Send<BooleanResponse>(
            HttpMethod.Put,
            $"products/{Escape(componentId)}/keep",
            new SetKeepOnImportRequest(keep)).Value;

    public BusinessSettings GetSettings() =>
        Send<BusinessSettings>(HttpMethod.Get, "settings");

    public void SaveSettings(BusinessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Send(HttpMethod.Put, "settings", settings);
    }

    public SavedQuote SaveQuote(
        SavedQuote? existing,
        string clientName,
        string clientPhone,
        string notes,
        IReadOnlyList<SavedQuoteItem> items,
        BusinessSettings companySnapshot) =>
        Send<SavedQuote>(
            HttpMethod.Post,
            "quotes",
            new SaveQuoteRequest(
                existing,
                clientName,
                clientPhone,
                notes,
                items,
                companySnapshot));

    public IReadOnlyList<SavedQuote> GetQuotes() =>
        Send<List<SavedQuote>>(HttpMethod.Get, "quotes");

    public bool DeleteQuote(Guid quoteId) =>
        Send<BooleanResponse>(
            HttpMethod.Delete,
            $"quotes/{Escape(quoteId.ToString("D"))}").Value;

    public async Task TestConnectionAsync()
    {
        using var response = await _httpClient
            .GetAsync("connection")
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content
                .ReadAsStringAsync()
                .ConfigureAwait(false);
            throw CreateHttpError(response, body);
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync()
            .ConfigureAwait(false);
        using var document = await JsonDocument
            .ParseAsync(stream)
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

    private T Send<T>(HttpMethod method, string path, object? content = null)
    {
        using var response = SendRequest(method, path, content);
        EnsureSuccess(response);
        return Deserialize<T>(response);
    }

    private void Send(HttpMethod method, string path, object? content = null)
    {
        using var response = SendRequest(method, path, content);
        EnsureSuccess(response);
    }

    private HttpResponseMessage SendRequest(
        HttpMethod method,
        string path,
        object? content = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (content is not null)
        {
            request.Content = JsonContent.Create(content, options: JsonOptions);
        }

        try
        {
            return _httpClient.Send(request);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                "Não foi possível acessar o servidor do BuildPC.",
                exception);
        }
        catch (TaskCanceledException exception)
        {
            throw new InvalidOperationException(
                "O servidor do BuildPC demorou demais para responder.",
                exception);
        }
    }

    private static T Deserialize<T>(HttpResponseMessage response)
    {
        using var stream = response.Content.ReadAsStream();
        return JsonSerializer.Deserialize<T>(stream, JsonOptions) ??
               throw new InvalidOperationException(
                   "O servidor do BuildPC devolveu uma resposta vazia.");
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        throw CreateHttpError(response, body);
    }

    private static InvalidOperationException CreateHttpError(
        HttpResponseMessage response,
        string body)
    {
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
        value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
}
