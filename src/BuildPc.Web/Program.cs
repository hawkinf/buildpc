using BuildPc.Core.Services;
using BuildPc.Web.Components;

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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    utc = DateTimeOffset.UtcNow
}));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
