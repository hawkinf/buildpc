namespace BuildPc.Api.Security;

/// <summary>
/// Registra quem alterou o quê na API.
/// </summary>
/// <remarks>
/// Antes não havia nenhum rastro: com produtos e orçamentos sendo apagados por
/// chamadas autenticadas, não era possível saber depois o que aconteceu. Só as
/// operações que mudam ou removem dados são registradas — leituras não, para o
/// log continuar legível.
/// </remarks>
public static class AuditLog
{
    /// <summary>
    /// Rotas que apagam ou alteram dados. As de leitura ficam fora de propósito.
    /// </summary>
    private static readonly string[] AuditedMethods =
    [
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    ];

    public static bool ShouldAudit(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return AuditedMethods.Contains(
            context.Request.Method,
            StringComparer.OrdinalIgnoreCase);
    }

    public static void Record(
        ILogger logger,
        HttpContext context,
        int statusCode)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(context);

        // Nível Information: o filtro global baixou Microsoft.AspNetCore para
        // Warning, então este log fica isolado do ruído por requisição.
        logger.LogInformation(
            "Auditoria: {Method} {Path} de {RemoteIp} resultou em {StatusCode}",
            context.Request.Method,
            context.Request.Path.Value,
            context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            statusCode);
    }
}
