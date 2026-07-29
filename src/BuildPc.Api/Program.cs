using System.Security.Cryptography;
using System.Text;
using BuildPc.Api.Database;
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

    var snapshot = SqliteSnapshotReader.Read(sqlitePath);
    app.Services
        .GetRequiredService<PostgresBuildPcRepository>()
        .ImportSnapshot(snapshot);
    Console.WriteLine(
        $"Migração concluída: {snapshot.Products.Count} produtos e " +
        $"{snapshot.Quotes.Count} orçamentos.");
    return 0;
}

var apiKey = builder.Configuration["BuildPc:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException(
        "A chave de acesso 'BuildPc:ApiKey' não foi configurada.");
}

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

app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/health"))
    {
        await next(context);
        return;
    }

    var suppliedKey = context.Request.Headers["X-BuildPc-Key"].ToString();
    if (!KeysMatch(suppliedKey, apiKey))
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

app.MapGet("/connection", () => Results.Ok(new
{
    status = "ok"
}));

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
        Results.Ok(repository.SaveQuote(
            request.Existing,
            request.ClientName,
            request.ClientPhone,
            request.Notes,
            request.Items,
            request.CompanySnapshot)));

app.MapDelete(
    "/quotes/{id:guid}",
    (Guid id, PostgresBuildPcRepository repository) =>
        Results.Ok(new BooleanResponse(repository.DeleteQuote(id))));

app.Run();
return 0;

static bool KeysMatch(string supplied, string expected)
{
    if (string.IsNullOrEmpty(supplied))
    {
        return false;
    }

    var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
    var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
    return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
}

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
