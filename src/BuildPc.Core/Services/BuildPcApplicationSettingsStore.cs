using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

public sealed record BuildPcApplicationConfiguration
{
    public BusinessSettings Application { get; init; } = new();
    public BuildPcApiSettings? ApiSettings { get; init; }
    public Dictionary<string, string> ImportSourceUrls { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BuildPcApplicationSettingsStore
{
    public const string FileName = "buildpc.config.json";
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly string _path;

    public BuildPcApplicationSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public string Path => _path;

    public static string DefaultPath =>
        System.IO.Path.Combine(AppContext.BaseDirectory, FileName);

    public BuildPcApplicationConfiguration? Load()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<SettingsDocument>(
                File.ReadAllText(_path),
                JsonOptions);
            if (document is null ||
                document.SchemaVersion is < 1 or > CurrentSchemaVersion)
            {
                return null;
            }

            BuildPcApiSettings? apiSettings = null;
            if (document.Server.Enabled)
            {
                var apiKey = BuildPcApiKeyProtector.Unprotect(
                    document.Server.EncryptedApiKey);
                apiSettings = new BuildPcApiSettings
                {
                    BaseUrl = document.Server.BaseUrl.Trim(),
                    ApiKey = apiKey
                };
                if (!apiSettings.IsValid())
                {
                    return null;
                }
            }

            return new BuildPcApplicationConfiguration
            {
                Application = document.Application ?? new BusinessSettings(),
                ApiSettings = apiSettings,
                ImportSourceUrls = NormalizeImportSourceUrls(
                    document.ImportSourceUrls)
            };
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

    public void Save(BuildPcApplicationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.ApiSettings is not null &&
            !configuration.ApiSettings.IsValid())
        {
            throw new InvalidOperationException(
                "Informe uma URL HTTPS e uma chave de acesso válidas.");
        }

        var server = configuration.ApiSettings is null
            ? new ServerSettingsDocument()
            : new ServerSettingsDocument
            {
                Enabled = true,
                BaseUrl = configuration.ApiSettings.BaseUrl,
                EncryptedApiKey = BuildPcApiKeyProtector.Protect(
                    configuration.ApiSettings.ApiKey)
            };
        var document = new SettingsDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            Application = configuration.Application,
            Server = server,
            ImportSourceUrls = NormalizeImportSourceUrls(
                configuration.ImportSourceUrls)
        };

        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(document, JsonOptions));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static Dictionary<string, string> NormalizeImportSourceUrls(
        IReadOnlyDictionary<string, string>? urls) =>
        urls?
            .Where(entry =>
                !string.IsNullOrWhiteSpace(entry.Key) &&
                !string.IsNullOrWhiteSpace(entry.Value))
            .ToDictionary(
                entry => entry.Key.Trim(),
                entry => entry.Value.Trim(),
                StringComparer.OrdinalIgnoreCase) ??
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record SettingsDocument
    {
        public int SchemaVersion { get; init; } = CurrentSchemaVersion;
        public BusinessSettings? Application { get; init; }
        public ServerSettingsDocument Server { get; init; } = new();
        public Dictionary<string, string> ImportSourceUrls { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record ServerSettingsDocument
    {
        public bool Enabled { get; init; }
        public string BaseUrl { get; init; } = string.Empty;
        public string EncryptedApiKey { get; init; } = string.Empty;
    }
}
