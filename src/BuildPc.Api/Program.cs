using System.Threading.RateLimiting;
using BuildPc.Api.Database;
using BuildPc.Api.Security;
using Microsoft.AspNetCore.RateLimiting;
using BuildPc.Core.Models;
using BuildPc.Core.Services;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;

if (TryGetSqliteBackupPaths(args, out var sourceDatabase, out var backupDatabase))
{
    SqliteSnapshotReader.CreateConsistentBackup(sourceDatabase, backupDatabase);
    Console.WriteLine($"Backup SQLite consistente criado em {backupDatabase}.");
    return 0;
}

var builder = WebApplication.CreateBuilder(args);
var connectionString =
    builder.Configuration.GetConnectionString("BuildPc") ??
    throw new InvalidOperationException(
        "A conexão PostgreSQL 'BuildPc' não foi configurada.");

// O padrão registra "Request starting" e "Request finished" para cada
// requisição. Com o rodapé do aplicativo consultando /connection a cada 30
// segundos, isso enche o journal do servidor sem informação útil.
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

// A chave de acesso é única e não expira. O limite por endereço encurta
// qualquer tentativa de descobri-la por força bruta, com folga muito acima do
// que o aplicativo usa (uma importação completa fica perto de 400 requisições).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1200,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// O Nginx da VPS libera corpos de até 64 MB (uma categoria grande importada
// de uma vez em /imports/replace); sem isto, o padrão do Kestrel (~28,6 MB)
// devolveria 413 antes mesmo de chegar ao endpoint, tornando o ajuste do
// Nginx inútil na prática.
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = 64 * 1024 * 1024);

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<PostgresBuildPcRepository>();

var app = builder.Build();
if (TryGetSqliteImportPath(args, out var sqlitePath))
{
    // A importação apaga produtos, orçamentos e configurações do PostgreSQL
    // antes de gravar o snapshot. Repetir o comando por engano destruiria a
    // base de produção, então a confirmação é obrigatória.
    if (!args.Any(argument =>
            string.Equals(argument, "--force", StringComparison.OrdinalIgnoreCase)))
    {
        Console.Error.WriteLine(
            "A importação APAGA todos os produtos, orçamentos e configurações " +
            "atuais do PostgreSQL antes de gravar o snapshot.");
        Console.Error.WriteLine(
            "Faça backup e repita o comando acrescentando --force para confirmar.");
        return 1;
    }

    var snapshot = await SqliteSnapshotReader.ReadAsync(sqlitePath);
    app.Services
        .GetRequiredService<PostgresBuildPcRepository>()
        .ImportSnapshot(snapshot);
    Console.WriteLine(
        $"Migração concluída: {snapshot.Products.Count} produtos, " +
        $"{snapshot.Quotes.Count} orçamentos e " +
        $"{snapshot.Templates.Count} modelos de montagem.");
    return 0;
}

// Aceita a chave em uso e, durante uma rotação, a anterior. Sem isso, trocar a
// chave exigiria atualizar servidor e clientes no mesmo instante.
var apiKeyValidator = ApiKeyValidator.FromConfiguration(builder.Configuration);

// Registrado antes do tratador de exceções de propósito: assim, quando um
// endpoint lança e a resposta vira 400/409/500 mais abaixo, o status final já
// está definido quando o "await next" retorna aqui e a tentativa é auditada
// do mesmo jeito que uma bem-sucedida. Antes, o registro ficava só depois do
// "next()" do middleware de autenticação (mais interno), e uma exceção
// pulava esse trecho inteiro — só o 401 explícito era de fato gravado.
app.Use(async (context, next) =>
{
    await next(context);

    var statusCode = context.Response.StatusCode;
    if (statusCode == StatusCodes.Status401Unauthorized ||
        AuditLog.ShouldAudit(context))
    {
        AuditLog.Record(app.Logger, context, statusCode);
    }
});

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features
            .Get<IExceptionHandlerFeature>()?
            .Error;
        var statusCode = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            // JSON inválido ou faltando uma propriedade "required": erro do
            // cliente, não do servidor. Antes caía no 500 genérico do fim.
            System.Text.Json.JsonException => StatusCodes.Status400BadRequest,
            Microsoft.AspNetCore.Http.BadHttpRequestException =>
                StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        var detail = statusCode == StatusCodes.Status500InternalServerError
            ? "O servidor não conseguiu concluir a operação."
            : exception?.Message;
        await Results.Problem(
                statusCode: statusCode,
                title: "Falha ao processar a solicitação.",
                detail: detail)
            .ExecuteAsync(context);
    });
});

app.UseRateLimiter();

app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/health"))
    {
        await next(context);
        return;
    }

    var suppliedKey = context.Request.Headers["X-BuildPc-Key"].ToString();
    if (!apiKeyValidator.IsValid(suppliedKey))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Acesso não autorizado.",
                detail: "A chave de acesso da API é inválida.")
            .ExecuteAsync(context);
        return;
    }

    await next(context);
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/connection", (PostgresBuildPcRepository repository) =>
{
    try
    {
        repository.CanConnect();
    }
    catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
    {
        // A API está de pé, mas o banco não responde: não é seguro dizer
        // ONLINE para o rodapé do Desktop, pois nenhuma operação real
        // funcionaria.
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Banco de dados indisponível.",
            detail: "A API está no ar, mas não conseguiu falar com o PostgreSQL.");
    }

    return Results.Ok(new { status = "ok" });
});

app.MapGet(
    "/products",
    (PostgresBuildPcRepository repository) => repository.GetAll());

app.MapPost(
    "/products",
    (PcComponent component, PostgresBuildPcRepository repository) =>
    {
        repository.Add(component);
        return Results.Created(
            $"/products/{Uri.EscapeDataString(component.Id)}",
            component);
    });

app.MapPut(
    "/products/{id}",
    (
        string id,
        PcComponent component,
        PostgresBuildPcRepository repository) =>
    {
        if (!string.Equals(id, component.Id, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new
            {
                detail = "O identificador da rota difere do identificador do produto."
            });
        }

        return Results.Ok(new BooleanResponse(repository.Update(component)));
    });

app.MapDelete(
    "/products/{id}",
    (string id, PostgresBuildPcRepository repository) =>
        Results.Ok(new BooleanResponse(repository.Delete(id))));

app.MapPost(
    "/products/delete",
    (
        DeleteProductsRequest request,
        PostgresBuildPcRepository repository) =>
        Results.Ok(new AffectedRowsResponse(
            repository.DeleteMany(request.Ids))));

app.MapPost(
    "/products/descriptions",
    (
        UpdateDescriptionsRequest request,
        PostgresBuildPcRepository repository) =>
        Results.Ok(new AffectedRowsResponse(
            repository.UpdateDescriptions(
                request.Ids,
                request.Description,
                request.Mode))));

app.MapPut(
    "/products/{id}/keep",
    (
        string id,
        SetKeepOnImportRequest request,
        PostgresBuildPcRepository repository) =>
        Results.Ok(new BooleanResponse(
            repository.SetKeepOnImport(id, request.Keep))));

app.MapPost(
    "/imports/replace",
    (
        ReplaceImportedProductsRequest request,
        PostgresBuildPcRepository repository) =>
        Results.Ok(repository.ReplaceImported(
            request.Category,
            request.Source,
            request.Components)));

app.MapGet(
    "/imports/last",
    (
        int category,
        string source,
        PostgresBuildPcRepository repository) =>
    {
        var importedAt = repository.GetLastImport(
            (ComponentCategory)category,
            source);
        return importedAt is null
            ? Results.NoContent()
            : Results.Ok(importedAt.Value);
    });

app.MapGet(
    "/imports/last-all",
    (PostgresBuildPcRepository repository) =>
        Results.Ok(repository.GetLastImports()));

app.MapGet(
    "/settings",
    (PostgresBuildPcRepository repository) => repository.GetSettings());

app.MapPut(
    "/settings",
    (
        BusinessSettings settings,
        PostgresBuildPcRepository repository) =>
    {
        repository.SaveSettings(settings);
        return Results.NoContent();
    });

app.MapGet(
    "/quotes",
    (PostgresBuildPcRepository repository) => repository.GetQuotes());

app.MapPost(
    "/quotes",
    (
        SaveQuoteRequest request,
        PostgresBuildPcRepository repository) =>
        Results.Ok(repository.SaveQuote(request.Existing, request.Draft)));

app.MapDelete(
    "/quotes/{id:guid}",
    (Guid id, PostgresBuildPcRepository repository) =>
        Results.Ok(new BooleanResponse(repository.DeleteQuote(id))));

app.MapPut(
    "/products/{id}/favorite",
    (
        string id,
        SetFavoriteRequest request,
        PostgresBuildPcRepository repository) =>
        Results.Ok(new BooleanResponse(
            repository.SetFavorite(id, request.Favorite))));

app.MapGet(
    "/products/{id}/price-history",
    (string id, PostgresBuildPcRepository repository) =>
        Results.Ok(repository.GetPriceHistory(id)));

app.MapGet(
    "/templates",
    (PostgresBuildPcRepository repository) =>
        Results.Ok(repository.GetTemplates()));

app.MapPost(
    "/templates",
    (AssemblyTemplate template, PostgresBuildPcRepository repository) =>
        Results.Ok(repository.SaveTemplate(template)));

app.MapDelete(
    "/templates/{id:guid}",
    (Guid id, PostgresBuildPcRepository repository) =>
        Results.Ok(new BooleanResponse(repository.DeleteTemplate(id))));

app.Run();
return 0;

static bool TryGetSqliteImportPath(
    IReadOnlyList<string> arguments,
    out string path)
{
    for (var index = 0; index < arguments.Count - 1; index++)
    {
        if (string.Equals(
                arguments[index],
                "--import-sqlite",
                StringComparison.OrdinalIgnoreCase))
        {
            path = Path.GetFullPath(arguments[index + 1]);
            return true;
        }
    }

    path = string.Empty;
    return false;
}

static bool TryGetSqliteBackupPaths(
    IReadOnlyList<string> arguments,
    out string sourcePath,
    out string destinationPath)
{
    for (var index = 0; index < arguments.Count - 2; index++)
    {
        if (string.Equals(
                arguments[index],
                "--backup-sqlite",
                StringComparison.OrdinalIgnoreCase))
        {
            sourcePath = Path.GetFullPath(arguments[index + 1]);
            destinationPath = Path.GetFullPath(arguments[index + 2]);
            return true;
        }
    }

    sourcePath = string.Empty;
    destinationPath = string.Empty;
    return false;
}
