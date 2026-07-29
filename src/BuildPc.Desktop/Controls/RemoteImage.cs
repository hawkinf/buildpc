using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using BuildPc.Desktop.Services;

namespace BuildPc.Desktop.Controls;

public sealed class RemoteImage : Image
{
    private const int MaximumImageBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Teto do cache compartilhado de imagens. Cobre com folga o que uma tela
    /// de catálogo exibe, sem crescer indefinidamente durante a execução.
    /// </summary>
    private const long MaximumCacheBytes = 96L * 1024 * 1024;

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly BoundedImageCache Cache = new(MaximumCacheBytes);

    /// <summary>
    /// Downloads em andamento, para várias linhas que mostram a mesma imagem
    /// não dispararem requisições repetidas.
    /// </summary>
    private static readonly Dictionary<string, Task<byte[]?>> InFlight =
        new(StringComparer.OrdinalIgnoreCase);

    private Bitmap? _ownedBitmap;
    private int _loadVersion;

    public static readonly StyledProperty<string?> UrlProperty =
        AvaloniaProperty.Register<RemoteImage, string?>(nameof(Url));

    static RemoteImage()
    {
        UrlProperty.Changed.AddClassHandler<RemoteImage>(
            (control, change) => control.LoadImage(change.NewValue as string));
    }

    public string? Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    private async void LoadImage(string? url)
    {
        var version = ++_loadVersion;
        ClearImage();

        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        string cacheKey;
        Func<Task<byte[]?>> read;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
        {
            cacheKey = uri.AbsoluteUri;
            read = () => DownloadAsync(cacheKey);
        }
        else
        {
            var localPath = uri?.IsFile == true ? uri.LocalPath : url;
            if (!Path.IsPathFullyQualified(localPath))
            {
                return;
            }

            cacheKey = $"local:{localPath}";
            read = () => ReadLocalAsync(localPath);
        }

        var bytes = await GetOrLoadAsync(cacheKey, read);
        if (bytes is null || version != _loadVersion)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (version != _loadVersion)
            {
                return;
            }

            try
            {
                using var stream = new MemoryStream(bytes, writable: false);
                _ownedBitmap = new Bitmap(stream);
                Source = _ownedBitmap;
            }
            catch
            {
                ClearImage();
            }
        });
    }

    /// <summary>
    /// Devolve os bytes do cache limitado, lendo apenas quando necessário e sem
    /// duplicar leituras simultâneas da mesma imagem.
    /// </summary>
    private static async Task<byte[]?> GetOrLoadAsync(
        string cacheKey,
        Func<Task<byte[]?>> read)
    {
        if (Cache.TryGet(cacheKey, out var cached))
        {
            return cached;
        }

        Task<byte[]?> task;
        lock (InFlight)
        {
            // Reconferir dentro do lock: outra linha pode ter concluído a
            // leitura entre a consulta acima e aqui.
            if (Cache.TryGet(cacheKey, out cached))
            {
                return cached;
            }

            if (!InFlight.TryGetValue(cacheKey, out task!))
            {
                task = read();
                InFlight[cacheKey] = task;
            }
        }

        try
        {
            var bytes = await task;
            Cache.Set(cacheKey, bytes);
            return bytes;
        }
        finally
        {
            lock (InFlight)
            {
                InFlight.Remove(cacheKey);
            }
        }
    }

    private static async Task<byte[]?> DownloadAsync(string url)
    {
        try
        {
            using var response = await HttpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentLength > MaximumImageBytes)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            return bytes.Length <= MaximumImageBytes ? bytes : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]?> ReadLocalAsync(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaximumImageBytes)
            {
                return null;
            }

            return await File.ReadAllBytesAsync(path);
        }
        catch
        {
            return null;
        }
    }

    private void ClearImage()
    {
        Source = null;
        _ownedBitmap?.Dispose();
        _ownedBitmap = null;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) BuildPC/1.0");
        return client;
    }
}
