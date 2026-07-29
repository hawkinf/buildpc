using BuildPc.Desktop.Services;

namespace BuildPc.Core.Tests;

public sealed class BoundedImageCacheTests
{
    [Fact]
    public void KeepsEntriesWhileUnderTheLimit()
    {
        var cache = new BoundedImageCache(1000);

        cache.Set("a", new byte[300]);
        cache.Set("b", new byte[300]);

        Assert.True(cache.TryGet("a", out var first));
        Assert.Equal(300, first!.Length);
        Assert.True(cache.TryGet("b", out _));
        Assert.Equal(600, cache.CurrentBytes);
    }

    [Fact]
    public void EvictsTheLeastRecentlyUsedEntryWhenOverTheLimit()
    {
        var cache = new BoundedImageCache(1000);
        cache.Set("a", new byte[400]);
        cache.Set("b", new byte[400]);

        // Ler "a" faz de "b" o menos recentemente usado.
        Assert.True(cache.TryGet("a", out _));
        cache.Set("c", new byte[400]);

        Assert.True(cache.TryGet("a", out _));
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
        Assert.Equal(800, cache.CurrentBytes);
    }

    [Fact]
    public void NeverExceedsTheConfiguredLimit()
    {
        var cache = new BoundedImageCache(1000);

        for (var index = 0; index < 50; index++)
        {
            cache.Set($"imagem-{index}", new byte[300]);
        }

        Assert.True(cache.CurrentBytes <= 1000);
        Assert.Equal(3, cache.Count);
    }

    [Fact]
    public void ReplacingAnEntryDoesNotDoubleCountItsSize()
    {
        var cache = new BoundedImageCache(1000);

        cache.Set("a", new byte[300]);
        cache.Set("a", new byte[500]);

        Assert.Equal(500, cache.CurrentBytes);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void RemembersFailedReadsSoTheyAreNotRetriedOnEveryScroll()
    {
        var cache = new BoundedImageCache(1000);

        cache.Set("quebrada", null);

        Assert.True(cache.TryGet("quebrada", out var bytes));
        Assert.Null(bytes);
        Assert.Equal(0, cache.CurrentBytes);
    }

    [Fact]
    public void RejectsAnEntryLargerThanTheWholeLimit()
    {
        var cache = new BoundedImageCache(1000);
        cache.Set("pequena", new byte[200]);

        cache.Set("gigante", new byte[5000]);

        // A imagem grande não entra e não derruba o que já estava em cache.
        Assert.False(cache.TryGet("gigante", out _));
        Assert.True(cache.TryGet("pequena", out _));
        Assert.Equal(200, cache.CurrentBytes);
    }
}
