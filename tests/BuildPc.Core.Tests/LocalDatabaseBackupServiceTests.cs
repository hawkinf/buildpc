using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

public sealed class LocalDatabaseBackupServiceTests
{
    [Fact]
    public void CreatesAConsistentCopyThatCanBeOpened()
    {
        using var workspace = new Workspace();
        workspace.SeedProduct("produto-1");

        var backup = workspace.Service.CreateDailyBackup(Day(1));

        Assert.NotNull(backup);
        Assert.True(File.Exists(backup));

        // A cópia precisa abrir e conter os dados, não só existir.
        var restored = new ComponentCatalogRepository(backup);
        Assert.Contains(restored.GetAll(), product => product.Id == "produto-1");
    }

    [Fact]
    public void OnlyOneBackupPerDay()
    {
        using var workspace = new Workspace();
        workspace.SeedProduct("produto-1");

        var first = workspace.Service.CreateDailyBackup(Day(1));
        var second = workspace.Service.CreateDailyBackup(Day(1));

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Single(workspace.Service.ListBackups());
    }

    [Fact]
    public void KeepsOnlyTheConfiguredNumberOfBackups()
    {
        using var workspace = new Workspace(keepCount: 3);
        workspace.SeedProduct("produto-1");

        for (var day = 1; day <= 6; day++)
        {
            workspace.Service.CreateDailyBackup(Day(day));
        }

        var backups = workspace.Service.ListBackups();
        Assert.Equal(3, backups.Count);

        // Sobram as três mais recentes.
        Assert.All(
            backups,
            path => Assert.Contains(
                Path.GetFileNameWithoutExtension(path)[^2..],
                new[] { "04", "05", "06" }));
    }

    [Fact]
    public void MissingDatabaseIsNotAnError()
    {
        using var workspace = new Workspace(seedDatabase: false);

        Assert.Null(workspace.Service.CreateDailyBackup(Day(1)));
        Assert.Empty(workspace.Service.ListBackups());
    }

    [Fact]
    public void ListBackupsReturnsNewestFirst()
    {
        using var workspace = new Workspace();
        workspace.SeedProduct("produto-1");
        workspace.Service.CreateDailyBackup(Day(1));
        workspace.Service.CreateDailyBackup(Day(2));
        workspace.Service.CreateDailyBackup(Day(3));

        var backups = workspace.Service.ListBackups();

        Assert.Equal(3, backups.Count);
        Assert.EndsWith("03.db", backups[0], StringComparison.Ordinal);
        Assert.EndsWith("01.db", backups[^1], StringComparison.Ordinal);
    }

    private static DateTimeOffset Day(int day) =>
        new(2026, 8, day, 9, 0, 0, TimeSpan.FromHours(-3));

    private sealed class Workspace : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-backup-{Guid.NewGuid():N}");

        public Workspace(int keepCount = 7, bool seedDatabase = true)
        {
            Directory.CreateDirectory(_root);
            DatabasePath = Path.Combine(_root, "catalogo.db");
            if (seedDatabase)
            {
                _ = new ComponentCatalogRepository(DatabasePath);
            }

            Service = new LocalDatabaseBackupService(
                DatabasePath,
                Path.Combine(_root, "backups"),
                keepCount);
        }

        public string DatabasePath { get; }
        public LocalDatabaseBackupService Service { get; }

        public void SeedProduct(string id)
        {
            var repository = new ComponentCatalogRepository(DatabasePath);
            repository.Add(new PcComponent
            {
                Id = id,
                Category = ComponentCategory.Processor,
                Name = $"Produto {id}",
                Brand = "Teste",
                Description = "Produto de teste",
                Price = 100m
            });
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
