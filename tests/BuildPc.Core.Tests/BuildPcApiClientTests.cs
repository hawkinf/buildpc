using System.Net;
using System.Text;
using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

public sealed class BuildPcApiClientTests
{
    [Fact]
    public async Task GetAllSendsApiKeyAndDeserializesProducts()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                "https://buildpc.example/products",
                request.RequestUri?.ToString());
            Assert.Equal(
                "secret",
                request.Headers.GetValues("X-BuildPc-Key").Single());
            return JsonResponse(
                """
                [{
                    "id":"product-1",
                    "category":0,
                    "name":"Produto",
                    "brand":"Marca",
                    "description":"Descrição",
                    "price":199.90
                }]
                """);
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://buildpc.example/")
        };
        using var client = new BuildPcApiClient(httpClient, "secret");

        var products = await client.GetAllAsync();

        var product = Assert.Single(products);
        Assert.Equal("product-1", product.Id);
        Assert.Equal(199.90m, product.Price);
    }

    [Fact]
    public async Task ServerProblemDetailIsReturnedAsOperationError()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(
                """{"title":"Acesso não autorizado.","detail":"Chave inválida."}""",
                HttpStatusCode.Unauthorized));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://buildpc.example/")
        };
        using var client = new BuildPcApiClient(httpClient, "invalid");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetSettingsAsync());

        Assert.Equal("Chave inválida.", exception.Message);
    }

    [Fact]
    public async Task TestConnectionAcceptsAuthenticatedBuildPcResponse()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(
                "https://buildpc.example/connection",
                request.RequestUri?.ToString());
            return JsonResponse("""{"status":"ok"}""");
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://buildpc.example/")
        };
        using var client = new BuildPcApiClient(httpClient, "secret");

        await client.TestConnectionAsync();
    }

    [Fact]
    public async Task ReplaceImportedAsyncDeserializesTheServerResponse()
    {
        // Reproduz um crash real: ImportReplaceResult tinha dois construtores
        // públicos (o principal e uma sobrecarga de conveniência nunca usada),
        // e o System.Text.Json não conseguia decidir qual usar — toda
        // importação pela API (não só um caso isolado) derrubava o programa
        // com "Deserialization of types without... a singular parameterized
        // constructor... is not supported" assim que a resposta chegava.
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(
                "https://buildpc.example/imports/replace",
                request.RequestUri?.ToString());
            return JsonResponse(
                """
                {
                    "imported": 55,
                    "removed": 50,
                    "kept": 2,
                    "importedAt": "2026-07-30T08:00:00Z",
                    "priceChanges": [
                        {
                            "componentId": "cpu-1",
                            "name": "Ryzen 5 5500",
                            "previousPrice": 700.00,
                            "currentPrice": 750.90
                        }
                    ],
                    "disappeared": 1
                }
                """);
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://buildpc.example/")
        };
        using var client = new BuildPcApiClient(httpClient, "secret");

        var result = await client.ReplaceImportedAsync(
            ComponentCategory.Processor,
            "kabum",
            []);

        Assert.Equal(55, result.Imported);
        Assert.Equal(1, result.Disappeared);
        var change = Assert.Single(result.PriceChanges);
        Assert.Equal("cpu-1", change.ComponentId);
        Assert.True(change.IsIncrease);
    }

    private static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json")
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));

        protected override HttpResponseMessage Send(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            responseFactory(request);
    }
}
