using System.Text.Json;

namespace BuildPc.Core.Services;

public sealed record BuildPcApiSettings
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;

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
