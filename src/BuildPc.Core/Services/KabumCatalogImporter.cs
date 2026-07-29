using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

public sealed partial class KabumCatalogImporter
{
    private const int MaximumPages = 200;

    /// <summary>Tentativas por página antes de desistir dela.</summary>
    private const int MaximumAttemptsPerPage = 4;

    /// <summary>
    /// Pausa entre páginas. Duzentas requisições em rajada fazem a loja
    /// responder com 429 e derrubar a importação inteira.
    /// </summary>
    private static readonly TimeSpan DelayBetweenPages =
        TimeSpan.FromMilliseconds(350);

    private readonly HttpClient _httpClient;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public KabumCatalogImporter(HttpClient httpClient)
        : this(httpClient, Task.Delay)
    {
    }

    /// <param name="delay">
    /// Injetável para os testes não esperarem de verdade pelos backoffs.
    /// </param>
    internal KabumCatalogImporter(
        HttpClient httpClient,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _httpClient = httpClient;
        _delay = delay;
    }

    public async Task<IReadOnlyList<PcComponent>> FetchAsync(
        string catalogUrl,
        ComponentCategory category,
        CancellationToken cancellationToken = default,
        IProgress<KabumImportProgress>? progress = null)
    {
        var components = new Dictionary<string, PcComponent>(
            StringComparer.OrdinalIgnoreCase);
        var pageFingerprints = new HashSet<string>(StringComparer.Ordinal);

        for (var pageNumber = 1; pageNumber <= MaximumPages; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pageNumber > 1)
            {
                await _delay(DelayBetweenPages, cancellationToken);
            }

            progress?.Report(new(
                pageNumber,
                components.Count,
                $"Baixando a página {pageNumber}..."));
            var pageUrl = WithPageNumber(catalogUrl, pageNumber);

            using var response = await SendWithRetryAsync(
                pageUrl,
                pageNumber,
                components.Count,
                progress,
                cancellationToken);
            if (response is null)
            {
                // A página falhou mesmo após as tentativas. Se já houver
                // produtos, é melhor entregar o que foi lido do que perder a
                // categoria inteira.
                if (components.Count > 0)
                {
                    break;
                }

                throw new HttpRequestException(
                    "A loja não respondeu à primeira página do catálogo.");
            }

            if (pageNumber > 1 &&
                response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            {
                break;
            }

            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var page = ParseCatalogPageHtml(html, category);
            if (page.RawProductCount == 0)
            {
                break;
            }

            if (!pageFingerprints.Add(page.Fingerprint))
            {
                break;
            }

            foreach (var component in page.Components)
            {
                components[component.Id] = component;
            }

            progress?.Report(new(
                pageNumber,
                components.Count,
                $"Página {pageNumber} concluída • {components.Count} produtos encontrados."));
        }

        if (components.Count == 0)
        {
            throw new InvalidDataException("Nenhum produto válido foi encontrado no catálogo.");
        }

        return components.Values.ToList();
    }

    /// <summary>
    /// Baixa uma página repetindo em falhas temporárias, com espera crescente e
    /// respeitando <c>Retry-After</c>. Devolve <c>null</c> quando as tentativas
    /// se esgotam.
    /// </summary>
    private async Task<HttpResponseMessage?> SendWithRetryAsync(
        string pageUrl,
        int pageNumber,
        int productCount,
        IProgress<KabumImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HttpResponseMessage? response = null;
            TimeSpan? retryAfter = null;
            try
            {
                response = await SendAsync(pageUrl, cancellationToken);
                if (!IsTransientFailure(response.StatusCode))
                {
                    return response;
                }

                retryAfter = response.Headers.RetryAfter?.Delta;
                response.Dispose();
            }
            catch (HttpRequestException)
            {
                response?.Dispose();
                if (attempt >= MaximumAttemptsPerPage)
                {
                    return null;
                }
            }

            if (attempt >= MaximumAttemptsPerPage)
            {
                return null;
            }

            var wait = retryAfter ?? TimeSpan.FromMilliseconds(700 * (1 << (attempt - 1)));
            progress?.Report(new(
                pageNumber,
                productCount,
                $"A loja recusou a página {pageNumber}. Nova tentativa em " +
                $"{Math.Ceiling(wait.TotalSeconds)}s ({attempt} de " +
                $"{MaximumAttemptsPerPage - 1})..."));
            await _delay(wait, cancellationToken);
        }
    }

    private static bool IsTransientFailure(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests
            or HttpStatusCode.RequestTimeout
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private async Task<HttpResponseMessage> SendAsync(
        string catalogUrl,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, catalogUrl);
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/138.0 Safari/537.36");
        request.Headers.AcceptLanguage.ParseAdd("pt-BR,pt;q=0.9");
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    public static IReadOnlyList<PcComponent> ParseCatalogHtml(
        string html,
        ComponentCategory category)
    {
        var page = ParseCatalogPageHtml(html, category);
        if (page.Components.Count == 0)
        {
            throw new InvalidDataException("Nenhum produto válido foi encontrado na página.");
        }

        return page.Components;
    }

    private static CatalogPage ParseCatalogPageHtml(
        string html,
        ComponentCategory category)
    {
        var match = NextDataRegex().Match(html);
        if (!match.Success)
        {
            throw new InvalidDataException("A página não contém os dados esperados do catálogo.");
        }

        var nextDataJson = WebUtility.HtmlDecode(match.Groups["json"].Value);
        using var nextData = JsonDocument.Parse(nextDataJson);

        var catalogJson = nextData.RootElement
            .GetProperty("props")
            .GetProperty("pageProps")
            .GetProperty("data")
            .GetString();

        if (string.IsNullOrWhiteSpace(catalogJson))
        {
            throw new InvalidDataException("O catálogo recebido está vazio.");
        }

        using var catalog = JsonDocument.Parse(catalogJson);
        var products = catalog.RootElement
            .GetProperty("catalogServer")
            .GetProperty("data");

        var components = new List<PcComponent>();
        var rawProductIds = new List<string>();
        foreach (var product in products.EnumerateArray())
        {
            rawProductIds.Add(ReadRawProductId(product));
            if (!TryReadProduct(product, category, out var component))
            {
                continue;
            }

            components.Add(component);
        }

        rawProductIds.Sort(StringComparer.Ordinal);
        return new CatalogPage(
            components,
            rawProductIds.Count,
            string.Join("|", rawProductIds));
    }

    private static string ReadRawProductId(JsonElement product)
    {
        if (product.TryGetProperty("code", out var code))
        {
            return code.ToString();
        }

        return product.TryGetProperty("name", out var name)
            ? name.GetString() ?? product.GetRawText()
            : product.GetRawText();
    }

    private static string WithPageNumber(string catalogUrl, int pageNumber)
    {
        if (!Uri.TryCreate(catalogUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("O link do catálogo é inválido.", nameof(catalogUrl));
        }

        var builder = new UriBuilder(uri);
        var queryParts = builder.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part =>
            {
                var separator = part.IndexOf('=');
                var key = separator < 0 ? part : part[..separator];
                return !string.Equals(
                    Uri.UnescapeDataString(key),
                    "page_number",
                    StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
        queryParts.Add($"page_number={pageNumber.ToString(CultureInfo.InvariantCulture)}");
        builder.Query = string.Join("&", queryParts);
        return builder.Uri.AbsoluteUri;
    }

    private sealed record CatalogPage(
        IReadOnlyList<PcComponent> Components,
        int RawProductCount,
        string Fingerprint);

    private static bool TryReadProduct(
        JsonElement product,
        ComponentCategory category,
        out PcComponent component)
    {
        component = null!;

        if (!product.TryGetProperty("code", out var codeElement) ||
            !codeElement.TryGetInt32(out var code) ||
            !product.TryGetProperty("name", out var nameElement))
        {
            return false;
        }

        var name = nameElement.GetString();
        var price = ReadCurrentPrice(product);
        if (string.IsNullOrWhiteSpace(name) ||
            price <= 0 ||
            !MatchesCategory(name, category))
        {
            return false;
        }

        var importedName = RemoveStoreMentions(name);
        if (string.IsNullOrWhiteSpace(importedName))
        {
            return false;
        }

        var brand = "Não informado";
        if (product.TryGetProperty("manufacturer", out var manufacturer) &&
            manufacturer.TryGetProperty("name", out var manufacturerName) &&
            !string.IsNullOrWhiteSpace(manufacturerName.GetString()))
        {
            brand = RemoveStoreMentions(manufacturerName.GetString()!);
            if (string.IsNullOrWhiteSpace(brand))
            {
                brand = "Não informado";
            }
        }

        var description = RemoveStoreMentions(
            ReadString(product, "tagDescription") ?? string.Empty);
        if (string.IsNullOrWhiteSpace(description))
        {
            description =
                $"Produto importado • código {code.ToString(CultureInfo.InvariantCulture)}";
        }

        component = new PcComponent
        {
            Id = $"kabum-{code.ToString(CultureInfo.InvariantCulture)}",
            Category = category,
            Name = importedName,
            Brand = brand.Trim(),
            Description = description.Trim(),
            Price = price,
            ImageUrl = ReadImageUrl(product),
            PowerWatts = category == ComponentCategory.PowerSupply
                ? ReadPowerWatts(name)
                : 0,
            Socket = category is ComponentCategory.Processor or ComponentCategory.Motherboard
                ? ReadSocket(name)
                : null,
            MemoryType = category is ComponentCategory.Motherboard or ComponentCategory.Memory
                ? ReadMemoryType(name)
                : null,
            FormFactor = category == ComponentCategory.Motherboard
                ? ReadMotherboardFormFactor(name)
                : null,
            SupportedSockets = category == ComponentCategory.Cooler
                ? ReadSupportedSockets(name)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            SupportedFormFactors = category == ComponentCategory.Case
                ? ReadSupportedFormFactors(name)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ImportSource = "kabum",
            KeepOnImport = false,
            IsUserDefined = false
        };
        return true;
    }

    private static string RemoveStoreMentions(string value)
    {
        var cleaned = StoreMentionRegex().Replace(value, " ");
        cleaned = WhitespaceRegex().Replace(cleaned, " ");
        cleaned = SpaceBeforePunctuationRegex().Replace(cleaned, "$1");
        return cleaned.Trim(' ', '•', '-', '|', ',', ';', '.', ':', '!');
    }

    private static decimal ReadCurrentPrice(JsonElement product)
    {
        if (product.TryGetProperty("offer", out var offer) &&
            offer.ValueKind == JsonValueKind.Object)
        {
            var offerPrice = ReadDecimal(offer, "priceWithDiscount");
            if (offerPrice > 0)
            {
                return offerPrice;
            }
        }

        var discountedPrice = ReadDecimal(product, "priceWithDiscount");
        return discountedPrice > 0 ? discountedPrice : ReadDecimal(product, "price");
    }

    private static decimal ReadDecimal(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetDecimal(out var number)
            ? number
            : 0;

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) ? value.GetString() : null;

    private static string? ReadImageUrl(JsonElement product)
    {
        foreach (var propertyName in new[] { "thumbnail", "image" })
        {
            var value = ReadString(product, propertyName);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            value = value.Trim();
            if (value.StartsWith("//", StringComparison.Ordinal))
            {
                value = $"https:{value}";
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https")
            {
                return uri.AbsoluteUri;
            }
        }

        return null;
    }

    private static string? ReadSocket(string name)
    {
        var match = SocketRegex().Match(name);
        return match.Success
            ? match.Value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant()
            : null;
    }

    private static bool MatchesCategory(
        string name,
        ComponentCategory category)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;
        return category switch
        {
            ComponentCategory.Processor =>
                name.Contains("Processador", comparison),
            ComponentCategory.Motherboard =>
                name.Contains("Placa-Mãe", comparison) ||
                name.Contains("Placa Mãe", comparison),
            ComponentCategory.Memory =>
                name.Contains("Memória", comparison),
            ComponentCategory.GraphicsCard =>
                name.Contains("Placa de Vídeo", comparison),
            ComponentCategory.Storage =>
                name.Contains("SSD", comparison) &&
                !IsStorageAccessory(name),
            ComponentCategory.HardDrive =>
                IsHardDrive(name) &&
                !name.Contains("SSD", comparison) &&
                !IsStorageAccessory(name),
            ComponentCategory.Monitor =>
                name.Contains("Monitor", comparison) &&
                !IsMonitorAccessory(name),
            ComponentCategory.Mouse =>
                name.Contains("Mouse", comparison) &&
                !name.Contains("Teclado", comparison) &&
                !IsMouseAccessory(name),
            ComponentCategory.Keyboard =>
                (name.Contains("Teclado", comparison) ||
                 name.Contains("Keyboard", comparison)) &&
                !name.Contains("Mouse", comparison) &&
                !IsKeyboardAccessory(name),
            ComponentCategory.PowerSupply =>
                name.Contains("Fonte", comparison),
            ComponentCategory.Case =>
                name.Contains("Gabinete", comparison),
            ComponentCategory.Cooler =>
                (name.Contains("Water Cooler", comparison) ||
                 name.Contains("Air Cooler", comparison) ||
                 name.StartsWith("Cooler ", comparison) ||
                 name.Contains("CPU Cooler", comparison)) &&
                !name.Contains("Pasta Térmica", comparison) &&
                !name.Contains("Ventoinha", comparison) &&
                !name.Contains("Suporte", comparison) &&
                !name.Contains("Controladora", comparison),
            _ => false
        };
    }

    private static bool IsHardDrive(string name)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;
        return name.StartsWith("HD ", comparison) ||
               name.Contains(" HD ", comparison) ||
               name.StartsWith("HDD ", comparison) ||
               name.Contains(" HDD ", comparison) ||
               name.Contains("Disco Rígido", comparison) ||
               name.Contains("Disco Rigido", comparison);
    }

    private static bool IsStorageAccessory(string name)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;
        return name.StartsWith("Case ", comparison) ||
               name.Contains("Gaveta", comparison) ||
               name.Contains("Adaptador", comparison) ||
               name.Contains("Conversor", comparison) ||
               name.Contains("Dock Station", comparison) ||
               name.Contains("Cabo ", comparison) ||
               name.Contains("Suporte ", comparison);
    }

    private static bool IsMonitorAccessory(string name)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;
        return name.Contains("Suporte", comparison) ||
               name.Contains("Braço", comparison) ||
               name.Contains("Braco", comparison) ||
               name.Contains("Cabo ", comparison) ||
               name.Contains("Película", comparison) ||
               name.Contains("Pelicula", comparison) ||
               name.Contains("Adaptador", comparison) ||
               name.Contains("Base para Monitor", comparison);
    }

    private static bool IsMouseAccessory(string name)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;
        return name.Contains("Mousepad", comparison) ||
               name.Contains("Mouse Pad", comparison) ||
               name.Contains("Suporte", comparison) ||
               name.Contains("Bungee", comparison) ||
               name.Contains("Cabo para Mouse", comparison);
    }

    private static bool IsKeyboardAccessory(string name)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;
        return name.Contains("Keycap", comparison) ||
               name.Contains("Teclas para", comparison) ||
               name.Contains("Apoio para", comparison) ||
               name.Contains("Suporte", comparison) ||
               name.Contains("Cabo para Teclado", comparison) ||
               name.StartsWith("Case ", comparison);
    }

    private static string? ReadMemoryType(string name)
    {
        var match = MemoryTypeRegex().Match(name);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    private static string? ReadMotherboardFormFactor(string name)
    {
        if (MicroAtxRegex().IsMatch(name))
        {
            return "mATX";
        }

        if (MiniItxRegex().IsMatch(name))
        {
            return "Mini-ITX";
        }

        return AtxRegex().IsMatch(name) ? "ATX" : null;
    }

    private static int ReadPowerWatts(string name)
    {
        var match = PowerRegex().Match(name);
        return match.Success &&
               int.TryParse(match.Groups["watts"].Value, CultureInfo.InvariantCulture, out var watts)
            ? watts
            : 0;
    }

    private static HashSet<string> ReadSupportedSockets(string name) =>
        new(
            SocketRegex()
                .Matches(name)
                .Select(match => match.Value
                    .Replace(" ", string.Empty, StringComparison.Ordinal)
                    .ToUpperInvariant()),
            StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> ReadSupportedFormFactors(string name)
    {
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (MicroAtxRegex().IsMatch(name))
        {
            supported.Add("mATX");
        }

        if (MiniItxRegex().IsMatch(name))
        {
            supported.Add("Mini-ITX");
        }

        if (AtxRegex().IsMatch(name))
        {
            supported.Add("ATX");
        }

        return supported;
    }

    [GeneratedRegex(
        @"\b(?:AM[245]|FM2|sTR5|WRX8|LGA\s*\d{3,4}|FCPGA\d+)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SocketRegex();

    [GeneratedRegex(
        @"\bDDR[2-5]\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MemoryTypeRegex();

    [GeneratedRegex(
        @"\b(?:M-?ATX|MICRO[- ]?ATX)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MicroAtxRegex();

    [GeneratedRegex(
        @"\bMINI[- ]?ITX\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MiniItxRegex();

    [GeneratedRegex(
        @"(?<!MICRO )(?<!MICRO-)(?<![-\w])ATX\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AtxRegex();

    [GeneratedRegex(
        @"(?<!\d)(?<watts>\d{3,4})\s*W\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PowerRegex();

    [GeneratedRegex(
        @"<script[^>]*id=[""']__NEXT_DATA__[""'][^>]*>(?<json>.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex NextDataRegex();

    [GeneratedRegex(
        @"\b(?:(?:no|na|do|da)\s+)?kabum\b!?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StoreMentionRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\s+([,.;:!?])", RegexOptions.CultureInvariant)]
    private static partial Regex SpaceBeforePunctuationRegex();
}

public sealed record KabumImportProgress(
    int PageNumber,
    int ProductCount,
    string Status);
