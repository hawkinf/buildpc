using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace BuildPc.Desktop.Controls;

public sealed class RemoteImage : Image
{
    private const int MaximumImageBytes = 5 * 1024 * 1024;
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly ConcurrentDictionary<string, Task<byte[]?>> Cache =
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

        Task<byte[]?> bytesTask;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
        {
            bytesTask = Cache.GetOrAdd(uri.AbsoluteUri, DownloadAsync);
        }
        else
        {
            var localPath = uri?.IsFile == true ? uri.LocalPath : url;
            if (!Path.IsPathFullyQualified(localPath))
            {
                return;
            }

            bytesTask = Cache.GetOrAdd(
                $"local:{localPath}",
                _ => ReadLocalAsync(localPath));
        }

        var bytes = await bytesTask;
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
