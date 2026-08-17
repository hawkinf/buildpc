using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Web.Services;

/// <summary>
/// Roda todo dia, no horário configurado, a mesma importação que o botão
/// "Importar tudo" da página de Importações dispara.
///
/// Por que dentro do app e não um robô clicando a tela: esta é uma aplicação
/// Blazor Server, então os botões não são requisições HTTP — são mensagens
/// SignalR para um componente que vive no servidor. Não há endpoint para um
/// cron chamar. Aqui a mesma lógica roda direto sobre os serviços, sem
/// navegador, sem sessão e sem depender de a tela continuar igual.
/// </summary>
public sealed class ImportacaoDiariaHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ImportacaoDiariaHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan HorarioPadrao = new(8, 0, 0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("BuildPc:ImportacaoDiaria:Habilitada", true))
        {
            logger.LogInformation("Importação diária desabilitada por configuração.");
            return;
        }

        var horario = LerHorario();
        var fuso = LerFuso();
        logger.LogInformation(
            "Importação diária ativa: {Horario} ({Fuso}).", horario, fuso.Id);

        while (!stoppingToken.IsCancellationRequested)
        {
            var espera = AteProxima(horario, fuso, DateTimeOffset.UtcNow);
            logger.LogInformation(
                @"Próxima importação em {Espera:hh\:mm\:ss}.", espera);
            try
            {
                await Task.Delay(espera, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await ImportarTudoAsync(stoppingToken);
            }
            catch (Exception excecao) when (excecao is not OperationCanceledException)
            {
                // Uma falha não pode matar o serviço: sem isto, um erro numa
                // manhã cancelaria a rotina para sempre, em silêncio.
                logger.LogError(excecao, "Importação diária falhou por inteiro.");
            }
        }
    }

    /// <summary>
    /// Mesma sequência de <c>ImportAllAsync</c> da página: para cada categoria
    /// com URL configurada, busca no Kabum e substitui o que estava importado.
    /// </summary>
    private async Task ImportarTudoAsync(CancellationToken cancellationToken)
    {
        // O KabumCatalogImporter é registrado com AddHttpClient, logo é
        // transiente — um BackgroundService é singleton e não pode injetá-lo
        // direto sem prender um HttpClient para sempre.
        using var escopo = scopeFactory.CreateScope();
        var provedor = escopo.ServiceProvider;
        var quotes = provedor.GetRequiredService<IQuoteRepository>();
        var catalogo = provedor.GetRequiredService<IComponentCatalogRepository>();
        var importador = provedor.GetRequiredService<KabumCatalogImporter>();

        var settings = await quotes.GetSettingsAsync();
        var categorias = settings.EffectiveProductCategories();
        var urlsGravadas = await quotes.GetImportSourceUrlsAsync();

        var total = 0;
        var falhas = 0;
        foreach (var definicao in categorias)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var categoria = definicao.Value;
            var url = ResolverUrl(categoria, urlsGravadas);
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            try
            {
                var componentes = await importador.FetchAsync(
                    url, categoria, cancellationToken);
                var resultado = await catalogo.ReplaceImportedAsync(
                    categoria,
                    ImportSourceDefaults.SourceKeyFor(categoria),
                    componentes);
                total++;
                logger.LogInformation(
                    "Importada {Categoria}: {Quantidade} componentes ({Resultado}).",
                    categoria, componentes.Count, resultado);
            }
            catch (Exception excecao) when (excecao is not OperationCanceledException)
            {
                // Por categoria: uma URL fora do ar não pode impedir as outras
                // onze de atualizarem.
                falhas++;
                logger.LogError(
                    excecao, "Falha ao importar {Categoria}.", categoria);
            }
        }

        logger.LogInformation(
            "Importação diária concluída: {Total} categoria(s), {Falhas} falha(s).",
            total, falhas);
    }

    /// <summary>
    /// O que estiver gravado no servidor vence; só cai para o padrão embutido
    /// quando a categoria nunca foi configurada — mesma regra da página.
    /// </summary>
    private static string? ResolverUrl(
        ComponentCategory categoria,
        IReadOnlyDictionary<string, string> urlsGravadas)
    {
        var chave = ImportSourceDefaults.ConfigurationKeyFor(categoria);
        if (urlsGravadas.TryGetValue(chave, out var gravada) &&
            !string.IsNullOrWhiteSpace(gravada))
        {
            return gravada;
        }

        return ImportSourceDefaults.Urls.GetValueOrDefault(categoria);
    }

    private TimeSpan LerHorario()
    {
        var bruto = configuration["BuildPc:ImportacaoDiaria:Horario"];
        return TimeSpan.TryParse(bruto, out var horario) ? horario : HorarioPadrao;
    }

    private TimeZoneInfo LerFuso()
    {
        var bruto = configuration["BuildPc:ImportacaoDiaria:Fuso"] ?? "America/Sao_Paulo";
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(bruto);
        }
        catch (Exception excecao)
            when (excecao is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // O servidor roda em UTC; sem o fuso o horário escorregaria três
            // horas sem aviso. Avisar alto é melhor que importar às 5h.
            logger.LogWarning(
                "Fuso '{Fuso}' não encontrado; usando UTC-3 fixo.", bruto);
            return TimeZoneInfo.CreateCustomTimeZone(
                "BuildPc-UTC-3", TimeSpan.FromHours(-3), "UTC-3", "UTC-3");
        }
    }

    /// <summary>Tempo até a próxima ocorrência do horário no fuso informado.</summary>
    internal static TimeSpan AteProxima(
        TimeSpan horario, TimeZoneInfo fuso, DateTimeOffset agoraUtc)
    {
        var agoraLocal = TimeZoneInfo.ConvertTime(agoraUtc, fuso);
        var alvo = new DateTimeOffset(
            agoraLocal.Year, agoraLocal.Month, agoraLocal.Day,
            horario.Hours, horario.Minutes, horario.Seconds,
            agoraLocal.Offset);

        if (alvo <= agoraLocal)
        {
            alvo = alvo.AddDays(1);
        }

        return alvo - agoraLocal;
    }
}
