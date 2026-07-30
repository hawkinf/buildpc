using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BuildPc.Core.Tests;

/// <summary>
/// Exercita a API pelo pipeline HTTP real (autenticação, roteamento,
/// tratamento de exceção), não só a lógica isolada de
/// <see cref="ApiKeyValidatorTests"/>.
/// </summary>
/// <remarks>
/// A string de conexão do Postgres é propositalmente inválida/inalcançável:
/// os cenários cobertos aqui (401 sem chave, JSON malformado, /health) nunca
/// chegam a abrir uma conexão real, porque o middleware de autenticação e o
/// parser de JSON rodam antes do repositório ser resolvido pelo DI.
/// </remarks>
public sealed class BuildPcApiIntegrationTests : IClassFixture<BuildPcApiFactory>
{
    private readonly BuildPcApiFactory _factory;

    public BuildPcApiIntegrationTests(BuildPcApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_IsPublicAndReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Products_WithoutKey_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Products_WithWrongKey_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-BuildPc-Key", "chave-errada");

        var response = await client.GetAsync("/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ImportSources_WithoutKey_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/settings/import-sources");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MalformedJson_ReturnsBadRequestNotServerError()
    {
        // Cobre o ajuste do lote 1 da auditoria: JsonException caía no 500
        // genérico antes, mesmo sendo claramente um erro do cliente.
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-BuildPc-Key", BuildPcApiFactory.TestApiKey);

        var response = await client.PostAsync(
            "/products",
            new StringContent("{ isto não é json válido", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class BuildPcApiFactory : WebApplicationFactory<Program>
{
    public const string TestApiKey = "chave-de-teste-integracao";

    public BuildPcApiFactory()
    {
        // WebApplicationBuilder.CreateBuilder já inclui variáveis de
        // ambiente nas suas fontes de configuração, então isto precisa
        // acontecer antes do host ser criado (no construtor, não em
        // ConfigureWebHost) para Program.cs enxergar os valores na primeira
        // leitura de builder.Configuration.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__BuildPc",
            "Host=127.0.0.1;Port=1;Database=inexistente;Username=x;Password=x;Timeout=1");
        Environment.SetEnvironmentVariable("BuildPc__ApiKey", TestApiKey);
    }
}
