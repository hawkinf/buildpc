using System.Text.Json;

namespace BuildPc.Core.Services;

/// <summary>
/// Verifica se um endereço responde como uma API do BuildPC.
/// </summary>
/// <remarks>
/// O rodapé refaz esta verificação a cada 30 segundos. Criar um
/// <see cref="HttpClient"/> por verificação deixaria conexões em
/// <c>TIME_WAIT</c> acumuladas durante horas de uso, então o cliente é único e
/// compartilhado: a URL e a chave viajam em cada requisição.
/// </remarks>
public static class BuildPcApiConnectionTester
{
    private static readonly HttpClient SharedClient = new(
        new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public static async Task TestAsync(
        BuildPcApiSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsValid())
        {
            throw new InvalidOperationException(
                "Informe uma URL HTTPS e uma chave de acesso válidas.");
        }

        var baseUrl = settings.BaseUrl.EndsWith('/')
            ? settings.BaseUrl
            : $"{settings.BaseUrl}/";
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(new Uri(baseUrl), "connection"));
        request.Headers.Add("X-BuildPc-Key", settings.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await SharedClient
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

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        ? "A chave de acesso da API foi recusada pelo servidor."
                        : "O servidor do BuildPC respondeu com o código " +
                          $"{(int)response.StatusCode}.");
            }

            await EnsureBuildPcResponseAsync(response, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task EnsureBuildPcResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var document = await JsonDocument
                .ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (document.RootElement.TryGetProperty("status", out var status) &&
                string.Equals(
                    status.GetString(),
                    "ok",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        catch (JsonException)
        {
            // Um corpo que não é JSON também indica que não é a API do BuildPC.
        }

        throw new InvalidOperationException(
            "O endereço respondeu, mas não parece ser uma API do BuildPC.");
    }
}
