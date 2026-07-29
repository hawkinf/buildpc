using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

public sealed class BuildPcApplicationSettingsStoreTests
{
    [Fact]
    public void SaveAndLoadRoundTripAllApplicationConfiguration()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var path = CreateTemporaryPath();
        const string apiKey = "api-key-that-must-never-appear-in-json";
        try
        {
            var expected = new BuildPcApplicationConfiguration
            {
                Application = new BusinessSettings
                {
                    GlobalMarginPercent = 35m,
                    CategoryMargins = new Dictionary<ComponentCategory, decimal>
                    {
                        [ComponentCategory.Processor] = 28m
                    },
                    ProductCategories =
                    [
                        new ProductCategoryDefinition
                        {
                            Value = ComponentCategory.Processor,
                            Name = "CPUs",
                            DisplayOrder = 1,
                            IsSystem = true
                        }
                    ],
                    CompanyName = "BuildPC Teste",
                    CompanyDocument = "00.000.000/0001-00",
                    CompanyPhone = "(11) 99999-9999",
                    CompanyEmail = "teste@example.com",
                    CompanyWebsite = "https://example.com",
                    CompanyAddress = "Rua de teste, 1",
                    LogoPath = "logo.png",
                    AdditionalQuoteInfo = "Garantia de teste",
                    ThemeMode = AppThemeMode.Dark
                },
                ApiSettings = new BuildPcApiSettings
                {
                    BaseUrl = "https://vps.example/buildpc-api/",
                    ApiKey = apiKey
                },
                ImportSourceUrls = new Dictionary<string, string>
                {
                    ["kabum:Processor"] =
                        "https://www.kabum.com.br/hardware/processadores?page_number=1"
                }
            };
            var store = new BuildPcApplicationSettingsStore(path);

            store.Save(expected);

            var json = File.ReadAllText(path);
            Assert.DoesNotContain(apiKey, json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"apiKey\"", json, StringComparison.Ordinal);
            Assert.Contains("\"encryptedApiKey\"", json, StringComparison.Ordinal);
            Assert.Contains("\"application\"", json, StringComparison.Ordinal);
            Assert.Contains("\"importSourceUrls\"", json, StringComparison.Ordinal);

            var actual = store.Load();
            Assert.NotNull(actual);
            Assert.Equal(expected.ApiSettings, actual.ApiSettings);
            Assert.Equal(
                expected.Application.GlobalMarginPercent,
                actual.Application.GlobalMarginPercent);
            Assert.Equal(expected.Application.CompanyName, actual.Application.CompanyName);
            Assert.Equal(expected.Application.ThemeMode, actual.Application.ThemeMode);
            Assert.Equal(
                expected.Application.CategoryMargins[ComponentCategory.Processor],
                actual.Application.CategoryMargins[ComponentCategory.Processor]);
            Assert.Equal(
                expected.ImportSourceUrls["kabum:Processor"],
                actual.ImportSourceUrls["kabum:Processor"]);
            Assert.Equal("CPUs", actual.Application.ProductCategories![0].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SaveWithoutServerStillCreatesDistributableConfiguration()
    {
        var path = CreateTemporaryPath();
        try
        {
            var store = new BuildPcApplicationSettingsStore(path);

            store.Save(new BuildPcApplicationConfiguration
            {
                Application = new BusinessSettings
                {
                    CompanyName = "Configuração local"
                }
            });

            var json = File.ReadAllText(path);
            Assert.Contains("\"enabled\": false", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"apiKey\"", json, StringComparison.Ordinal);

            var loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Null(loaded.ApiSettings);
            Assert.Equal("Configuração local", loaded.Application.CompanyName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadKeepsApplicationSettingsWhenApiKeyCannotBeDecrypted()
    {
        var path = CreateTemporaryPath();
        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "schemaVersion": 1,
                  "application": {
                    "globalMarginPercent": 42,
                    "companyName": "Loja preservada",
                    "companyPhone": "(11) 98888-7777"
                  },
                  "server": {
                    "enabled": true,
                    "baseUrl": "https://vps.example/buildpc-api/",
                    "encryptedApiKey": "dpapi-current-user:v1:chave-de-outro-usuario"
                  },
                  "importSourceUrls": {
                    "kabum:Processor": "https://www.kabum.com.br/hardware/processadores"
                  }
                }
                """);

            var loaded = new BuildPcApplicationSettingsStore(path).Load();

            Assert.NotNull(loaded);
            Assert.Null(loaded.ApiSettings);
            Assert.True(loaded.IsApiKeyUnreadable);
            Assert.Equal("Loja preservada", loaded.Application.CompanyName);
            Assert.Equal(42m, loaded.Application.GlobalMarginPercent);
            Assert.Equal(
                "https://www.kabum.com.br/hardware/processadores",
                loaded.ImportSourceUrls["kabum:Processor"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SaveKeepsUnreadableServerSectionUntouched()
    {
        var path = CreateTemporaryPath();
        const string encryptedKey = "dpapi-current-user:v1:chave-de-outro-usuario";
        try
        {
            File.WriteAllText(
                path,
                $$"""
                {
                  "schemaVersion": 1,
                  "application": { "companyName": "Antes" },
                  "server": {
                    "enabled": true,
                    "baseUrl": "https://vps.example/buildpc-api/",
                    "encryptedApiKey": "{{encryptedKey}}"
                  },
                  "importSourceUrls": {}
                }
                """);
            var store = new BuildPcApplicationSettingsStore(path);
            var loaded = store.Load()!;

            store.Save(loaded with
            {
                Application = loaded.Application with { CompanyName = "Depois" }
            });

            var json = File.ReadAllText(path);
            Assert.Contains(encryptedKey, json, StringComparison.Ordinal);
            Assert.Contains(
                "https://vps.example/buildpc-api/",
                json,
                StringComparison.Ordinal);
            Assert.Contains("\"enabled\": true", json, StringComparison.Ordinal);
            Assert.Equal("Depois", store.Load()!.Application.CompanyName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTemporaryPath() =>
        Path.Combine(
            Path.GetTempPath(),
            $"buildpc-application-settings-{Guid.NewGuid():N}.json");
}
