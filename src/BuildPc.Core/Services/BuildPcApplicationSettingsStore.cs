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

    /// <summary>
    /// Verdadeiro quando o arquivo pede o servidor, mas a chave gravada não pôde
    /// ser lida neste computador ou usuário do Windows. As demais configurações
    /// permanecem válidas e não devem ser descartadas.
    /// </summary>
    public bool IsApiKeyUnreadable { get; init; }

    /// <summary>
    /// Verdadeiro quando o servidor está sendo ignorado apenas nesta sessão,
    /// por escolha do usuário no aviso de início. O arquivo não pode perder o
    /// acesso já configurado.
    /// </summary>
    public bool IsServerBypassed { get; init; }

    internal bool MustKeepStoredServerSection =>
        ApiSettings is null && (IsApiKeyUnreadable || IsServerBypassed);
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

        SettingsDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<SettingsDocument>(
                File.ReadAllText(_path),
                JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (document is null ||
            document.SchemaVersion is < 1 or > CurrentSchemaVersion)
        {
            return null;
        }

        // Uma chave de API ilegível nunca pode descartar empresa, margens,
        // categorias e links já configurados: apenas o acesso ao servidor é
        // desativado até que a chave seja informada novamente.
        var apiSettings = TryReadApiSettings(document.Server);
        return new BuildPcApplicationConfiguration
        {
            Application = document.Application ?? new BusinessSettings(),
            ApiSettings = apiSettings,
            ImportSourceUrls = NormalizeImportSourceUrls(document.ImportSourceUrls),
            IsApiKeyUnreadable = document.Server.Enabled && apiSettings is null
        };
    }

    private static BuildPcApiSettings? TryReadApiSettings(
        ServerSettingsDocument server)
    {
        if (!server.Enabled)
        {
            return null;
        }

        try
        {
            var settings = new BuildPcApiSettings
            {
                BaseUrl = server.BaseUrl.Trim(),
                ApiKey = BuildPcApiKeyProtector.Unprotect(server.EncryptedApiKey)
            };
            return settings.IsValid() ? settings : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (PlatformNotSupportedException)
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

        ServerSettingsDocument server;
        if (configuration.ApiSettings is not null)
        {
            server = new ServerSettingsDocument
            {
                Enabled = true,
                BaseUrl = configuration.ApiSettings.BaseUrl,
                EncryptedApiKey = BuildPcApiKeyProtector.Protect(
                    configuration.ApiSettings.ApiKey)
            };
        }
        else if (configuration.MustKeepStoredServerSection)
        {
            // A chave pertence a outro usuário do Windows, ou o servidor foi
            // ignorado só nesta sessão. Preservar o bloco original evita que
            // salvar margens ou dados da empresa destrua o acesso configurado.
            server = ReadServerSectionFromDisk() ?? new ServerSettingsDocument();
        }
        else
        {
            server = new ServerSettingsDocument();
        }

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

    private ServerSettingsDocument? ReadServerSectionFromDisk()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            return JsonSerializer
                .Deserialize<SettingsDocument>(File.ReadAllText(_path), JsonOptions)?
                .Server;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
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
