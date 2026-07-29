namespace BuildPc.Desktop.Services;

/// <summary>
/// Cache de bytes de imagem em memória com teto de tamanho e descarte do item
/// usado menos recentemente.
/// </summary>
/// <remarks>
/// Um dicionário sem limite guardava até 5 MB por produto visitado e nunca
/// liberava nada: navegar por um catálogo importado de milhares de itens
/// consumia memória sem parar durante toda a execução.
/// </remarks>
public sealed class BoundedImageCache(long maximumBytes)
{
    private readonly Dictionary<string, Entry> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ordem de uso, do mais antigo para o mais recente.</summary>
    private readonly LinkedList<string> _usageOrder = new();

    private readonly object _gate = new();
    private long _currentBytes;

    public long CurrentBytes
    {
        get
        {
            lock (_gate)
            {
                return _currentBytes;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public bool TryGet(string key, out byte[]? bytes)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                bytes = null;
                return false;
            }

            _usageOrder.Remove(entry.UsageNode);
            _usageOrder.AddLast(entry.UsageNode);
            bytes = entry.Bytes;
            return true;
        }
    }

    /// <summary>
    /// Guarda o resultado de um download. Aceita <c>null</c> para lembrar que a
    /// imagem não pôde ser lida e não tentar de novo a cada rolagem.
    /// </summary>
    public void Set(string key, byte[]? bytes)
    {
        var size = bytes?.LongLength ?? 0;
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                _currentBytes -= existing.Size;
                _usageOrder.Remove(existing.UsageNode);
                _entries.Remove(key);
            }

            // Um item maior que o teto inteiro nunca entra: guardá-lo esvaziaria
            // o cache sem benefício.
            if (size > maximumBytes)
            {
                return;
            }

            var node = _usageOrder.AddLast(key);
            _entries[key] = new Entry(bytes, size, node);
            _currentBytes += size;
            EvictWhileOverLimit();
        }
    }

    private void EvictWhileOverLimit()
    {
        while (_currentBytes > maximumBytes && _usageOrder.First is { } oldest)
        {
            if (_entries.Remove(oldest.Value, out var evicted))
            {
                _currentBytes -= evicted.Size;
            }

            _usageOrder.RemoveFirst();
        }
    }

    private sealed record Entry(
        byte[]? Bytes,
        long Size,
        LinkedListNode<string> UsageNode);
}
