using System.Text.Json;
using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

public sealed class BuildPcApiSettingsTests
{
    [Fact]
    public void LoadAcceptsHttpsConfiguration()
    {
        var path = CreateTemporaryPath();
        try
        {
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(new
                {
                    baseUrl = "https://contaslite.hawk.com.br/buildpc-api/",
                    apiKey = "test-key"
                }));

            var settings = BuildPcApiSettings.Load(path);

            Assert.NotNull(settings);
            Assert.Equal(
                "https://contaslite.hawk.com.br/buildpc-api/",
                settings.BaseUrl);
            Assert.Equal("test-key", settings.ApiKey);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("http://servidor.exemplo/api/", "test-key")]
    [InlineData("https://servidor.exemplo/api/", "")]
    [InlineData("endereco-invalido", "test-key")]
    public void LoadRejectsUnsafeOrIncompleteConfiguration(
        string baseUrl,
        string apiKey)
    {
        var path = CreateTemporaryPath();
        try
        {
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(new
                {
                    baseUrl,
                    apiKey
                }));

            Assert.Null(BuildPcApiSettings.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTemporaryPath() =>
        Path.Combine(
            Path.GetTempPath(),
            $"buildpc-api-settings-{Guid.NewGuid():N}.json");
}
