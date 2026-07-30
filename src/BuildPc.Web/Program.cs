using System.Security.Claims;
using BuildPc.Core.Services;
using BuildPc.Web.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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

    if (!validator.IsValid(password))
    {
        return Results.Redirect(
            $"/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
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
        string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
}).DisableAntiforgery();

app.MapPost("/account/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
}).DisableAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
