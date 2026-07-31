using System.Text.Json;
using System.Net;
using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

public sealed class KabumCatalogImporterTests
{
    [Fact]
    public void ParseCatalogHtml_UsesOfferPriceAndNormalizesSocket()
    {
        var catalogJson = JsonSerializer.Serialize(new
        {
            catalogServer = new
            {
                data = new[]
                {
                    new
                    {
                        code = 123456,
                        name = "Processador Intel Core i7, LGA 1700",
                        tagDescription = "Processador importado",
                        manufacturer = new { name = "Intel" },
                        thumbnail = "https://images.example.com/processador_m.jpg",
                        image = "https://images.example.com/processador_original.jpg",
                        price = 1_299.90m,
                        priceWithDiscount = 1_199.90m,
                        offer = new { priceWithDiscount = 999.90m }
                    }
                }
            }
        });
        var nextDataJson = JsonSerializer.Serialize(new
        {
            props = new
            {
                pageProps = new
                {
                    data = catalogJson
                }
            }
        });
        var html =
            $"<html><script id=\"__NEXT_DATA__\" type=\"application/json\">{nextDataJson}</script></html>";

        var products = KabumCatalogImporter.ParseCatalogHtml(
            html,
            BuildPc.Core.Models.ComponentCategory.Processor);

        var processor = Assert.Single(products);
        Assert.Equal("kabum-123456", processor.Id);
        Assert.Equal("Intel", processor.Brand);
        Assert.Equal("LGA1700", processor.Socket);
        Assert.Equal(999.90m, processor.Price);
        Assert.Equal("https://images.example.com/processador_m.jpg", processor.ImageUrl);
    }

    [Fact]
    public void ParseCatalogHtml_HardDriveModeExcludesSsdsAndAccessories()
    {
        var html = CatalogHtml(
            Product(1, "HD Seagate Barracuda 2 TB 7200 RPM"),
            Product(2, "SSD Kingston NV3 1 TB"),
            Product(3, "Case Disco Rígido HD Externo 2.5 USB 3.0"));

        var products = KabumCatalogImporter.ParseCatalogHtml(
            html,
            ComponentCategory.HardDrive);

        var hardDrive = Assert.Single(products);
        Assert.Equal("kabum-1", hardDrive.Id);
        Assert.Equal(ComponentCategory.HardDrive, hardDrive.Category);
    }

    [Fact]
    public void ParseCatalogHtml_CoolersExcludeFansAndThermalPaste()
    {
        var html = CatalogHtml(
            Product(1, "Water Cooler Corsair H100 240mm"),
            Product(2, "Ventoinha Cooler Master 120mm"),
            Product(3, "Pasta Térmica para Cooler 5g"));

        var products = KabumCatalogImporter.ParseCatalogHtml(
            html,
            ComponentCategory.Cooler);

        var cooler = Assert.Single(products);
        Assert.Equal("kabum-1", cooler.Id);
    }

    [Fact]
    public void ParseCatalogHtml_SeparatesMousesAndKeyboardsFromCombos()
    {
        var html = CatalogHtml(
            Product(1, "Mouse Gamer Logitech G203 RGB"),
            Product(2, "Teclado Mecânico Redragon Kumara"),
            Product(3, "Combo Teclado e Mouse Logitech MK120"),
            Product(4, "Mousepad Gamer Grande"));

        var mouses = KabumCatalogImporter.ParseCatalogHtml(
            html,
            ComponentCategory.Mouse);
        var keyboards = KabumCatalogImporter.ParseCatalogHtml(
            html,
            ComponentCategory.Keyboard);

        Assert.Equal("kabum-1", Assert.Single(mouses).Id);
        Assert.Equal("kabum-2", Assert.Single(keyboards).Id);
    }

    [Fact]
    public void ParseCatalogHtml_MonitorsExcludeAccessories()
    {
        var html = CatalogHtml(
            Product(1, "Monitor Gamer LG UltraGear 27 polegadas"),
            Product(2, "Suporte Articulado para Monitor 17 a 32 polegadas"));

        var monitors = KabumCatalogImporter.ParseCatalogHtml(
            html,
            ComponentCategory.Monitor);

        Assert.Equal("kabum-1", Assert.Single(monitors).Id);
    }

    [Fact]
    public void ParseCatalogHtml_RemovesStoreNameFromImportedText()
    {
        var html = CatalogHtml(new
        {
            code = 99,
            name = "Mouse Gamer no KaBuM!",
            tagDescription = "Compre no Kabum! Melhor preço Kabum.",
            manufacturer = new { name = "Kabum" },
            priceWithDiscount = 100m
        });

        var mouse = Assert.Single(KabumCatalogImporter.ParseCatalogHtml(
            html,
            ComponentCategory.Mouse));

        Assert.Equal("Mouse Gamer", mouse.Name);
        Assert.Equal("Não informado", mouse.Brand);
        Assert.DoesNotContain(
            "kabum",
            mouse.Description,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Compre Melhor preço", mouse.Description);
    }

    [Fact]
    public void ParseCatalogHtml_RemovesStoreNameEvenWithoutASpaceBeforeIt()
    {
        // Achado em produção: a descrição real da Kabum às vezes cola a
        // preposição direto no nome da loja, sem espaço ("...encontra
        // noKaBuM"), e o regex antigo exigia espaço ali.
        var html = CatalogHtml(new
        {
            code = 99,
            name = "Monitor Gamer",
            tagDescription = "Tudo isso você encontra noKaBuM",
            priceWithDiscount = 100m
        });

        var monitor = Assert.Single(KabumCatalogImporter.ParseCatalogHtml(
            html,
            ComponentCategory.Monitor));

        Assert.DoesNotContain(
            "kabum",
            monitor.Description,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Tudo isso você encontra", monitor.Description);
    }

    [Fact]
    public async Task FetchAsync_ImportsEveryPageAndRemovesDuplicates()
    {
        var requestedPages = new List<int>();
        using var httpClient = new HttpClient(new TestHttpHandler(uri =>
        {
            var page = PageNumber(uri);
            requestedPages.Add(page);
            return page switch
            {
                1 => CatalogHtml(
                    Product(1, "Processador AMD Ryzen 5"),
                    Product(2, "Processador Intel Core i5")),
                2 => CatalogHtml(
                    Product(2, "Processador Intel Core i5"),
                    Product(3, "Processador AMD Ryzen 7")),
                _ => CatalogHtml()
            };
        }));
        var importer = new KabumCatalogImporter(httpClient);

        var products = await importer.FetchAsync(
            "https://www.kabum.com.br/hardware/processadores" +
            "?page_number=9&page_size=2&sort=most_searched",
            ComponentCategory.Processor);

        Assert.Equal([1, 2, 3], requestedPages);
        Assert.Equal(3, products.Count);
        Assert.Equal(3, products.Select(product => product.Id).Distinct().Count());
    }

    [Fact]
    public async Task FetchAsync_StopsWhenStoreRepeatsLastPage()
    {
        var requestedPages = new List<int>();
        var repeatedPage = CatalogHtml(Product(10, "Processador AMD Ryzen 9"));
        using var httpClient = new HttpClient(new TestHttpHandler(uri =>
        {
            requestedPages.Add(PageNumber(uri));
            return repeatedPage;
        }));
        var importer = new KabumCatalogImporter(httpClient);

        var products = await importer.FetchAsync(
            "https://www.kabum.com.br/hardware/processadores?page_size=60",
            ComponentCategory.Processor);

        Assert.Equal([1, 2], requestedPages);
        Assert.Single(products);
    }

    [Fact]
    public async Task FetchAsync_ReportsCurrentPageAndProductCount()
    {
        var updates = new List<KabumImportProgress>();
        using var httpClient = new HttpClient(new TestHttpHandler(uri =>
            PageNumber(uri) == 1
                ? CatalogHtml(
                    Product(21, "Processador AMD Ryzen 5"),
                    Product(22, "Processador Intel Core i7"))
                : CatalogHtml()));
        var importer = new KabumCatalogImporter(httpClient);

        var products = await importer.FetchAsync(
            "https://www.kabum.com.br/hardware/processadores?page_size=60",
            ComponentCategory.Processor,
            progress: new RecordingProgress<KabumImportProgress>(updates.Add));

        Assert.Equal(2, products.Count);
        Assert.Contains(updates, update =>
            update.PageNumber == 1 &&
            update.ProductCount == 2 &&
            update.Status.Contains("concluída", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(updates, update =>
            update.PageNumber == 2 &&
            update.Status.Contains("Baixando", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FetchAsync_CancelsTheCurrentRequest()
    {
        using var httpClient = new HttpClient(new DelayedHttpHandler());
        var importer = new KabumCatalogImporter(httpClient);
        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            importer.FetchAsync(
                "https://www.kabum.com.br/hardware/processadores?page_size=60",
                ComponentCategory.Processor,
                cancellation.Token));
    }

    private static object Product(int code, string name) =>
        new
        {
            code,
            name,
            tagDescription = "Produto importado",
            manufacturer = new { name = "Teste" },
            priceWithDiscount = 100m
        };

    private static string CatalogHtml(params object[] products)
    {
        var catalogJson = JsonSerializer.Serialize(new
        {
            catalogServer = new { data = products }
        });
        var nextDataJson = JsonSerializer.Serialize(new
        {
            props = new
            {
                pageProps = new { data = catalogJson }
            }
        });
        return $"<html><script id=\"__NEXT_DATA__\" type=\"application/json\">{nextDataJson}</script></html>";
    }

    private static int PageNumber(Uri uri)
    {
        var value = uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Single(part => string.Equals(
                Uri.UnescapeDataString(part[0]),
                "page_number",
                StringComparison.OrdinalIgnoreCase))[1];
        return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task FetchAsync_RetriesTransientFailuresBeforeGivingUpOnAPage()
    {
        var attempts = 0;
        var handler = new StatusHttpHandler(uri =>
        {
            if (PageNumber(uri) != 1)
            {
                return (HttpStatusCode.NotFound, string.Empty);
            }

            // As duas primeiras tentativas da página 1 são recusadas.
            return ++attempts <= 2
                ? (HttpStatusCode.TooManyRequests, string.Empty)
                : (HttpStatusCode.OK, CatalogHtml(Product(1, "Processador Um")));
        });
        var waits = new List<TimeSpan>();
        var importer = CreateImporter(handler, waits);

        var products = await importer.FetchAsync(
            "https://www.kabum.com.br/hardware/processadores",
            ComponentCategory.Processor);

        Assert.Equal(3, attempts);
        Assert.Single(products);
        // Espera crescente entre as tentativas.
        Assert.Equal(
            [TimeSpan.FromMilliseconds(700), TimeSpan.FromMilliseconds(1400)],
            waits.Take(2));
    }

    [Fact]
    public async Task FetchAsync_HonorsRetryAfterHeader()
    {
        var attempts = 0;
        var handler = new StatusHttpHandler(
            uri => PageNumber(uri) != 1
                ? (HttpStatusCode.NotFound, string.Empty)
                : ++attempts == 1
                    ? (HttpStatusCode.ServiceUnavailable, string.Empty)
                    : (HttpStatusCode.OK, CatalogHtml(Product(1, "Processador Um"))),
            retryAfter: TimeSpan.FromSeconds(5));
        var waits = new List<TimeSpan>();
        var importer = CreateImporter(handler, waits);

        await importer.FetchAsync(
            "https://www.kabum.com.br/hardware/processadores",
            ComponentCategory.Processor);

        Assert.Equal(TimeSpan.FromSeconds(5), waits[0]);
    }

    [Fact]
    public async Task FetchAsync_KeepsProductsAlreadyReadWhenALaterPageKeepsFailing()
    {
        var handler = new StatusHttpHandler(uri => PageNumber(uri) switch
        {
            1 => (HttpStatusCode.OK, CatalogHtml(Product(1, "Processador Um"))),
            2 => (HttpStatusCode.OK, CatalogHtml(Product(2, "Processador Dois"))),
            // A página 3 nunca responde: a categoria não pode ser perdida.
            _ => (HttpStatusCode.InternalServerError, string.Empty)
        });
        var importer = CreateImporter(handler, []);

        var products = await importer.FetchAsync(
            "https://www.kabum.com.br/hardware/processadores",
            ComponentCategory.Processor);

        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task FetchAsync_RetriesOnRequestTimeoutBeforeGivingUpOnAPage()
    {
        // Timeout do HttpClient (TaskCanceledException sem o usuário ter
        // pedido cancelamento) antes só derrubava a página inteira na
        // primeira falha, sem passar pela espera crescente do restante da
        // política de novas tentativas.
        var attempts = 0;
        var handler = new ThrowThenSucceedHandler(uri =>
        {
            if (PageNumber(uri) != 1)
            {
                return (HttpStatusCode.NotFound, string.Empty);
            }

            attempts++;
            if (attempts == 1)
            {
                throw new TaskCanceledException("Tempo esgotado.");
            }

            return (HttpStatusCode.OK, CatalogHtml(Product(1, "Processador Um")));
        });
        var importer = CreateImporter(handler, []);

        var products = await importer.FetchAsync(
            "https://www.kabum.com.br/hardware/processadores",
            ComponentCategory.Processor);

        Assert.Equal(2, attempts);
        Assert.Single(products);
    }

    [Fact]
    public async Task FetchAsync_FailsWhenTheFirstPageNeverResponds()
    {
        var handler = new StatusHttpHandler(
            _ => (HttpStatusCode.ServiceUnavailable, string.Empty));
        var importer = CreateImporter(handler, []);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            importer.FetchAsync(
                "https://www.kabum.com.br/hardware/processadores",
                ComponentCategory.Processor));
    }

    private static KabumCatalogImporter CreateImporter(
        HttpMessageHandler handler,
        List<TimeSpan> waits) =>
        new(
            new HttpClient(handler),
            (wait, _) =>
            {
                waits.Add(wait);
                return Task.CompletedTask;
            });

    [Theory]
    // Cada caso remove uma parte da estrutura que a loja pode reorganizar.
    [InlineData("""{"props":{"pageProps":{}}}""")]
    [InlineData("""{"props":{}}""")]
    [InlineData("""{}""")]
    [InlineData("""{"props":{"pageProps":{"data":123}}}""")]
    [InlineData("""{"props":{"pageProps":{"data":"{}"}}}""")]
    [InlineData("""{"props":{"pageProps":{"data":"{\"catalogServer\":{}}"}}}""")]
    [InlineData("""{"props":{"pageProps":{"data":"{\"catalogServer\":{\"data\":5}}"}}}""")]
    public void ParseCatalogHtml_ReportsUnexpectedStructureAsInvalidData(string nextDataJson)
    {
        var html =
            $"<html><script id=\"__NEXT_DATA__\" type=\"application/json\">{nextDataJson}</script></html>";

        // Precisa ser InvalidDataException: é o que a tela trata para avisar que
        // a loja mudou o formato da página.
        Assert.Throws<InvalidDataException>(() =>
            KabumCatalogImporter.ParseCatalogHtml(html, ComponentCategory.Processor));
    }

    private sealed class StatusHttpHandler(
        Func<Uri, (HttpStatusCode Status, string Body)> responseFactory,
        TimeSpan? retryAfter = null)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var (status, body) = responseFactory(request.RequestUri!);
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            };
            if (retryAfter is not null)
            {
                response.Headers.RetryAfter =
                    new System.Net.Http.Headers.RetryConditionHeaderValue(
                        retryAfter.Value);
            }

            return Task.FromResult(response);
        }
    }

    private sealed class ThrowThenSucceedHandler(
        Func<Uri, (HttpStatusCode Status, string Body)> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var (status, body) = responseFactory(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            });
        }
    }

    private sealed class TestHttpHandler(Func<Uri, string> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseFactory(request.RequestUri!))
            });
    }

    private sealed class RecordingProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class DelayedHttpHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
