using System.Text.Json;
using System.Security.Cryptography;

namespace BuildPc.Core.Services;

public sealed record BuildPcApiSettings
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;

    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!IsValid())
        {
            throw new InvalidOperationException(
                "Informe uma URL HTTPS e uma chave de acesso válidas.");
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var document = new PersistedSettings
            {
                BaseUrl = BaseUrl,
                EncryptedApiKey = BuildPcApiKeyProtector.Protect(ApiKey)
            };
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(document, JsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static void Disable(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static BuildPcApiSettings? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<PersistedSettings>(
                File.ReadAllText(path),
                JsonOptions);
            if (document is null)
            {
                return null;
            }

            var apiKey = !string.IsNullOrWhiteSpace(document.EncryptedApiKey)
                ? BuildPcApiKeyProtector.Unprotect(document.EncryptedApiKey)
                : document.ApiKey;
            var settings = new BuildPcApiSettings
            {
                BaseUrl = document.BaseUrl,
                ApiKey = apiKey ?? string.Empty
            };
            return settings?.IsValid() == true ? settings : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public bool IsValid() =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback) &&
        !string.IsNullOrWhiteSpace(ApiKey);

    private sealed record PersistedSettings
    {
        public string BaseUrl { get; init; } = string.Empty;
        public string EncryptedApiKey { get; init; } = string.Empty;

        // Compatibilidade somente de leitura com o servidor.json antigo.
        public string? ApiKey { get; init; }
    }
}
