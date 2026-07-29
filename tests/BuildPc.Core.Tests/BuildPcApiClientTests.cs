using System.Net;
using System.Text;
using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

public sealed class BuildPcApiClientTests
{
    [Fact]
    public void GetAllSendsApiKeyAndDeserializesProducts()
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

        var products = client.GetAll();

        var product = Assert.Single(products);
        Assert.Equal("product-1", product.Id);
        Assert.Equal(199.90m, product.Price);
    }

    [Fact]
    public void ServerProblemDetailIsReturnedAsOperationError()
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

        var exception = Assert.Throws<InvalidOperationException>(
            () => client.GetSettings());

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
