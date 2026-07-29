using BuildPc.Core.Models;
using BuildPc.Desktop.Services;

namespace BuildPc.Core.Tests;

public sealed class ProductImageStoreTests
{
    [Fact]
    public void PersistCopiesTheChosenPhotoIntoTheStore()
    {
        using var workspace = new Workspace();
        var source = workspace.CreateExternalFile("foto.png");

        var stored = workspace.Store.Persist("produto-1", source);

        Assert.NotNull(stored);
        Assert.StartsWith(workspace.ImagesDirectory, stored, StringComparison.Ordinal);
        Assert.True(File.Exists(stored));
        // O arquivo original do usuário nunca é movido nem apagado.
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void PersistKeepsRemoteUrlsUntouched()
    {
        using var workspace = new Workspace();

        var stored = workspace.Store.Persist(
            "produto-1",
            "https://images.example.com/foto.jpg");

        Assert.Equal("https://images.example.com/foto.jpg", stored);
    }

    [Fact]
    public void ReplacingAPhotoDeletesTheOneThatIsNoLongerUsed()
    {
        using var workspace = new Workspace();
        var first = workspace.Store.Persist(
            "produto-1",
            workspace.CreateExternalFile("antiga.png"))!;
        var second = workspace.Store.Persist(
            "produto-1",
            workspace.CreateExternalFile("nova.png"))!;

        workspace.Store.DeleteIfUnused(first, second);

        Assert.False(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    [Fact]
    public void KeepsThePhotoWhenItIsStillTheCurrentOne()
    {
        using var workspace = new Workspace();
        var stored = workspace.Store.Persist(
            "produto-1",
            workspace.CreateExternalFile("foto.png"))!;

        workspace.Store.DeleteIfUnused(stored, stored);

        Assert.True(File.Exists(stored));
    }

    [Fact]
    public void NeverDeletesFilesOutsideTheStore()
    {
        using var workspace = new Workspace();
        var external = workspace.CreateExternalFile("do-usuario.png");

        workspace.Store.DeleteIfUnused(external);

        // Uma foto fora da pasta do BuildPC pertence ao usuário.
        Assert.True(File.Exists(external));
    }

    [Fact]
    public void IgnoresRemoteUrlsAndInvalidPathsWhenDeleting()
    {
        using var workspace = new Workspace();

        workspace.Store.DeleteIfUnused("https://images.example.com/foto.jpg");
        workspace.Store.DeleteIfUnused(string.Empty);
        workspace.Store.DeleteIfUnused((string?)null);
    }

    [Fact]
    public void DeletesEveryPhotoOfTheRemovedProducts()
    {
        using var workspace = new Workspace();
        var first = workspace.Store.Persist(
            "produto-1",
            workspace.CreateExternalFile("um.png"))!;
        var second = workspace.Store.Persist(
            "produto-2",
            workspace.CreateExternalFile("dois.png"))!;

        workspace.Store.DeleteIfUnused(
        [
            Component("produto-1", first),
            Component("produto-2", second),
            Component("produto-3", "https://images.example.com/tres.jpg")
        ]);

        Assert.False(File.Exists(first));
        Assert.False(File.Exists(second));
    }

    [Fact]
    public void PersistRejectsAPhotoLargerThanTheAllowedSize()
    {
        using var workspace = new Workspace();
        var oversized = Path.Combine(workspace.ExternalDirectory, "grande.png");
        File.WriteAllBytes(oversized, new byte[5 * 1024 * 1024 + 1]);

        Assert.Throws<IOException>(() =>
            workspace.Store.Persist("produto-1", oversized));
    }

    [Fact]
    public void PersistFailsWhenTheChosenFileDoesNotExist()
    {
        using var workspace = new Workspace();

        Assert.Throws<IOException>(() =>
            workspace.Store.Persist(
                "produto-1",
                Path.Combine(workspace.ExternalDirectory, "inexistente.png")));
    }

    private static PcComponent Component(string id, string? imageUrl) =>
        new()
        {
            Id = id,
            Category = ComponentCategory.Processor,
            Name = id,
            Brand = "Teste",
            Description = "Produto de teste",
            Price = 100m,
            ImageUrl = imageUrl
        };

    private sealed class Workspace : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-imagens-{Guid.NewGuid():N}");

        public Workspace()
        {
            ImagesDirectory = Path.Combine(_root, "imagens-produtos");
            ExternalDirectory = Path.Combine(_root, "escolhidas");
            Directory.CreateDirectory(ImagesDirectory);
            Directory.CreateDirectory(ExternalDirectory);
            Store = new ProductImageStore(ImagesDirectory);
        }

        public string ImagesDirectory { get; }
        public string ExternalDirectory { get; }
        public ProductImageStore Store { get; }

        public string CreateExternalFile(string fileName)
        {
            var path = Path.Combine(ExternalDirectory, fileName);
            File.WriteAllBytes(path, [1, 2, 3, 4]);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
