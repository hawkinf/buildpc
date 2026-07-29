using System.Text.Json;

namespace BuildPc.Core.Services;

public sealed record BuildPcApiSettings
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

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
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(this, JsonOptions));
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
            var settings = JsonSerializer.Deserialize<BuildPcApiSettings>(
                File.ReadAllText(path),
                JsonOptions);
            return settings?.IsValid() == true ? settings : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public bool IsValid() =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback) &&
        !string.IsNullOrWhiteSpace(ApiKey);
}
