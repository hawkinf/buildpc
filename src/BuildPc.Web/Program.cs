using System.Security.Claims;
using BuildPc.Core.Models;
using BuildPc.Core.Services;
using BuildPc.Web.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Mesma convenção de configuração da BuildPc.Api: variáveis de ambiente
// BuildPc__BaseUrl / BuildPc__ApiKey (arquivo /etc/buildpc-web.env em
// produção), sem persistência local de chave como no Desktop.
builder.Services.AddSingleton(sp =>
{
    var settings = new BuildPcApiSettings
    {
        BaseUrl = sp.GetRequiredService<IConfiguration>()["BuildPc:BaseUrl"] ?? string.Empty,
        ApiKey = sp.GetRequiredService<IConfiguration>()["BuildPc:ApiKey"] ?? string.Empty
    };
    return new BuildPcApiClient(settings);
});
builder.Services.AddSingleton<IComponentCatalogRepository>(
    sp => sp.GetRequiredService<BuildPcApiClient>());
builder.Services.AddSingleton<IQuoteRepository>(
    sp => sp.GetRequiredService<BuildPcApiClient>());
builder.Services.AddSingleton<IAssemblyTemplateRepository>(
    sp => sp.GetRequiredService<BuildPcApiClient>());

builder.Services.AddSingleton(sp =>
    StaffPasswordValidator.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddScoped<BuildPc.Web.Services.RevealAccessState>();

// KabumCatalogImporter roda inteiramente no servidor (é assim que o Desktop
// já faz hoje, só que na máquina do usuário) — sem CORS envolvido, porque
// não é o navegador quem chama o Kabum. Tipo tipado em vez de "new
// HttpClient()" direto: evita esgotamento de sockets num processo de longa
// duração como este (diferente do app desktop, que abre uma vez por sessão).
builder.Services.AddHttpClient<KabumCatalogImporter>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
});

// O usuário de sistema que roda o serviço em produção não tem diretório
// home (endurecimento do systemd) — sem um caminho explícito, o Data
// Protection cai para uma chave efêmera e todo reinício do serviço
// invalidaria o cookie de login de todo mundo. Sem BuildPc:DataProtectionKeyPath
// configurado (dev local), usa o comportamento padrão do ASP.NET Core.
var dataProtectionKeyPath = builder.Configuration["BuildPc:DataProtectionKeyPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
}

// O Nginx (mesma VPS, 127.0.0.1) faz o TLS e repassa por HTTP simples; sem
// isto, o Kestrel enxergaria toda requisição como HTTP e o cookie de sessão
// nunca ganharia o atributo Secure em produção.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(System.Net.IPAddress.Loopback);
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        // Circuito e cookie curtos de propósito: a tela mostra custo/margem
        // em um dispositivo potencialmente compartilhado da loja.
        options.ExpireTimeSpan = TimeSpan.FromHours(10);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    utc = DateTimeOffset.UtcNow
}));

// Endpoints simples (não componentes Razor) porque SignInAsync/SignOutAsync
// gravam o cookie no cabeçalho da resposta HTTP — dentro de um circuito
// Blazor Server interativo essa resposta já foi iniciada, então o cookie
// nunca seria aplicado. .DisableAntiforgery() porque não há sessão prévia
// (login) ou porque o logout deve funcionar mesmo com o token expirado.
app.MapPost("/account/login", async (HttpContext context, StaffPasswordValidator validator) =>
{
    var form = await context.Request.ReadFormAsync();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    // Senha errada não mostra erro nem some no vazio: cai direto na tabela
    // de preços pública, como se a pessoa nunca tivesse tentado entrar como
    // equipe. Pedido explícito do usuário -- "entrar" na área da equipe é
    // opcional, a tabela pública sempre é o destino de quem não tem a senha.
    if (!validator.IsValid(password))
    {
        return Results.LocalRedirect("/consulta");
    }

    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "equipe")],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(10)
        });

    return Results.LocalRedirect(
        string.IsNullOrEmpty(returnUrl) || returnUrl == "/" ? "/precos" : returnUrl);
}).DisableAntiforgery();

app.MapPost("/account/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
}).DisableAntiforgery();

// Endpoint minimal-API dedicado (não parte do circuito Blazor Server): o
// download de um PDF exige uma resposta HTTP de verdade, e reconstrói a
// mesma tabela da tela a partir dos parâmetros da URL (PriceTableRowBuilder
// é a mesma lógica usada por Precos.razor, então tela e PDF nunca divergem).
app.MapGet(
    "/pdf/tabela-precos",
    async (
        IComponentCatalogRepository catalogRepository,
        IQuoteRepository quoteRepository,
        string? category,
        string? search,
        string? sort,
        string? priceMode) =>
    {
        var catalog = await catalogRepository.GetAllAsync();
        var settings = await quoteRepository.GetSettingsAsync();
        var categoryFilter = Enum.TryParse<ComponentCategory>(category, out var parsedCategory)
            ? parsedCategory
            : (ComponentCategory?)null;
        var sortMode = Enum.TryParse<PriceTableSortMode>(sort, out var parsedSort)
            ? parsedSort
            : PriceTableSortMode.NameAscending;
        var showSalePrice = !string.Equals(priceMode, "custo", StringComparison.OrdinalIgnoreCase);

        var rows = PriceTableRowBuilder.Build(
            catalog,
            settings,
            categoryFilter,
            search,
            sortMode,
            showSalePrice);
        var categoryName = categoryFilter is null
            ? "Todos"
            : settings.EffectiveProductCategories()
                .FirstOrDefault(definition => definition.Value == categoryFilter.Value)?.Name ??
              categoryFilter.Value.ToString();

        var document = new ProductPriceTableDocument(
            "Tabela de preços",
            showSalePrice ? "Preço de venda" : "Custo",
            categoryName,
            search ?? string.Empty,
            settings.CompanyName,
            DateTimeOffset.Now,
            rows);

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-web-tabela-{Guid.NewGuid():N}.pdf");
        try
        {
            await new ProductPriceTablePdfService().ExportAsync(document, tempPath);
            var bytes = await File.ReadAllBytesAsync(tempPath);
            return Results.File(bytes, "application/pdf", "tabela-de-precos.pdf");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    })
    .RequireAuthorization();

// Mesmo raciocínio do endpoint acima: GetQuotesAsync não tem uma versão
// "por id" na API, então filtra a lista completa em memória — a lista de
// orçamentos não chega perto do volume que justificaria um endpoint novo.
app.MapGet(
    "/pdf/orcamento/{id:guid}",
    async Task<IResult> (Guid id, IQuoteRepository quoteRepository, bool descricoes = true) =>
    {
        var quotes = await quoteRepository.GetQuotesAsync();
        var quote = quotes.FirstOrDefault(quote => quote.Id == id);
        if (quote is null)
        {
            return Results.NotFound();
        }

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-web-orcamento-{Guid.NewGuid():N}.pdf");
        try
        {
            await Task.Run(() => new QuotePdfService().Export(quote, tempPath, descricoes));
            var bytes = await File.ReadAllBytesAsync(tempPath);
            return Results.File(
                bytes,
                "application/pdf",
                $"orcamento-{quote.Number:000000}.pdf");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    })
    .RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
