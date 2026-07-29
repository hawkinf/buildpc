using BuildPc.Core.Models;
using BuildPc.Core.Services;
using Microsoft.Data.Sqlite;

namespace BuildPc.Core.Tests;

public sealed class ComponentCatalogRepositoryTests
{
    [Fact]
    public void AddedProduct_IsAvailableAfterReload()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"buildpc-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "catalogo.db");

        try
        {
            var repository = new ComponentCatalogRepository(filePath);
            repository.Add(new PcComponent
            {
                Id = "custom-test",
                Category = ComponentCategory.Processor,
                Name = "Produto de teste",
                Brand = "BuildPC",
                Description = "Componente persistido",
                Price = 999.90m,
                ImageUrl = "https://images.example.com/produto.jpg",
                PowerWatts = 65,
                Socket = "AM5"
            });

            var reloaded = new ComponentCatalogRepository(filePath).GetAll();

            Assert.Contains(reloaded, component => component.Id == "custom-test");
            Assert.Equal(
                "https://images.example.com/produto.jpg",
                reloaded.Single(component => component.Id == "custom-test").ImageUrl);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ExistingDatabase_AddsImageColumnWithoutLosingProducts()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"buildpc-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "catalogo.db");
        Directory.CreateDirectory(directory);

        try
        {
            using (var connection = new SqliteConnection(
                       $"Data Source={filePath};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE products (
                        id TEXT PRIMARY KEY COLLATE NOCASE,
                        category INTEGER NOT NULL,
                        name TEXT NOT NULL COLLATE NOCASE,
                        brand TEXT NOT NULL,
                        description TEXT NOT NULL,
                        price_cents INTEGER NOT NULL,
                        power_watts INTEGER NOT NULL,
                        socket TEXT NULL,
                        memory_type TEXT NULL,
                        form_factor TEXT NULL,
                        supported_sockets TEXT NOT NULL,
                        supported_form_factors TEXT NOT NULL,
                        import_source TEXT NULL,
                        keep_on_import INTEGER NOT NULL DEFAULT 0,
                        is_user_defined INTEGER NOT NULL DEFAULT 0
                    );
                    INSERT INTO products (
                        id, category, name, brand, description, price_cents, power_watts,
                        supported_sockets, supported_form_factors, keep_on_import, is_user_defined)
                    VALUES (
                        'legacy-product', 0, 'Produto antigo', 'Teste', 'Sem imagem',
                        10000, 0, '[]', '[]', 0, 0);
                    """;
                command.ExecuteNonQuery();
            }

            var repository = new ComponentCatalogRepository(filePath);
            var legacy = repository.GetAll().Single(component =>
                component.Id == "legacy-product");

            Assert.Null(legacy.ImageUrl);
            Assert.Equal("Produto antigo", legacy.Name);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ReplaceImported_RemovesPreviousItemsButPreservesMarkedItem()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"buildpc-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "catalogo.db");

        try
        {
            var repository = new ComponentCatalogRepository(filePath);
            var kept = CreateProcessor("kabum-keep", "Processador Z", 900m);
            var replaceable = CreateProcessor("kabum-remove", "Processador B", 800m);
            repository.Add(new PcComponent
            {
                Id = "custom-manual",
                Category = ComponentCategory.Processor,
                Name = "Processador Manual",
                Brand = "BuildPC",
                Description = "Não deve ser removido pela importação",
                Price = 700m,
                PowerWatts = 65,
                Socket = "AM5"
            });
            repository.ReplaceImported(
                ComponentCategory.Processor,
                "kabum",
                [kept, replaceable]);
            Assert.True(repository.SetKeepOnImport(kept.Id, true));

            var replacement = CreateProcessor("kabum-new", "Processador A", 750m);
            var result = repository.ReplaceImported(
                ComponentCategory.Processor,
                "kabum",
                [replacement]);
            var imported = new ComponentCatalogRepository(filePath)
                .GetAll()
                .Where(component => component.Id.StartsWith("kabum-", StringComparison.Ordinal))
                .ToList();

            Assert.Equal(1, result.Imported);
            Assert.Equal(1, result.Removed);
            Assert.Equal(1, result.Kept);
            Assert.Contains(imported, component => component.Id == kept.Id && component.KeepOnImport);
            Assert.Contains(imported, component => component.Id == replacement.Id);
            Assert.DoesNotContain(imported, component => component.Id == replaceable.Id);
            Assert.Contains(
                repository.GetAll(),
                component => component.Id == "custom-manual" && component.IsUserDefined);
            Assert.Equal(
                imported.OrderBy(component => component.Name, StringComparer.CurrentCultureIgnoreCase),
                imported);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ReplaceImported_RecordsDateAndPreservesOtherSourcesInSameCategory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"buildpc-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "catalogo.db");

        try
        {
            var repository = new ComponentCatalogRepository(filePath);
            repository.ReplaceImported(
                ComponentCategory.Processor,
                "fornecedor-antigo",
                [CreateProcessor("old-source", "Produto antigo", 500m)]);

            var before = DateTimeOffset.UtcNow;
            var result = repository.ReplaceImported(
                ComponentCategory.Processor,
                "kabum",
                [CreateProcessor("kabum-current", "Produto atual", 600m)]);
            var after = DateTimeOffset.UtcNow;
            var lastImport = repository.GetLastImport(
                ComponentCategory.Processor,
                "kabum");

            Assert.Contains(repository.GetAll(), component => component.Id == "old-source");
            Assert.Equal(0, result.Removed);
            Assert.NotNull(lastImport);
            Assert.InRange(lastImport.Value, before, after);
            Assert.Equal(lastImport, result.ImportedAt);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ExistingHardDriveImports_AreMigratedToDedicatedCategory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"buildpc-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "catalogo.db");

        try
        {
            var repository = new ComponentCatalogRepository(filePath);
            repository.ReplaceImported(
                ComponentCategory.Storage,
                "kabum-hd",
                [
                    new PcComponent
                    {
                        Id = "kabum-hd-test",
                        Category = ComponentCategory.Storage,
                        Name = "HD de teste",
                        Brand = "Teste",
                        Description = "Disco rígido",
                        Price = 300m
                    }
                ]);

            var migratedRepository = new ComponentCatalogRepository(filePath);
            var migrated = migratedRepository.GetAll().Single(component =>
                component.Id == "kabum-hd-test");

            Assert.Equal(ComponentCategory.HardDrive, migrated.Category);
            Assert.NotNull(migratedRepository.GetLastImport(
                ComponentCategory.HardDrive,
                "kabum-hd"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void CatalogCategoryOrder_PlacesCoolersAndHardDrivesAsRequested()
    {
        var categories = Enum.GetValues<ComponentCategory>()
            .OrderBy(ComponentCategoryInfo.DisplayOrder)
            .ToList();

        Assert.Equal(
            [
                ComponentCategory.Processor,
                ComponentCategory.Cooler,
                ComponentCategory.Motherboard,
                ComponentCategory.Memory,
                ComponentCategory.GraphicsCard,
                ComponentCategory.HardDrive,
                ComponentCategory.Storage,
                ComponentCategory.PowerSupply,
                ComponentCategory.Case,
                ComponentCategory.Monitor,
                ComponentCategory.Mouse,
                ComponentCategory.Keyboard
            ],
            categories);
    }

    [Fact]
    public void UpdateAndDelete_ModifyCatalogPersistently()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"buildpc-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "catalogo.db");
        try
        {
            var repository = new ComponentCatalogRepository(filePath);
            var product = new PcComponent
            {
                Id = "editable-product",
                Category = ComponentCategory.Memory,
                Name = "Memória original",
                Brand = "Teste",
                Description = "Descrição original",
                Price = 100m
            };
            repository.Add(product);

            Assert.True(repository.Update(product with
            {
                Name = "Memória editada",
                Description = "Descrição editada",
                Price = 125m
            }));
            var edited = repository.GetAll().Single(item => item.Id == product.Id);
            Assert.Equal("Memória editada", edited.Name);
            Assert.Equal("Descrição editada", edited.Description);
            Assert.Equal(125m, edited.Price);

            Assert.True(repository.Delete(product.Id));
            Assert.DoesNotContain(
                new ComponentCatalogRepository(filePath).GetAll(),
                item => item.Id == product.Id);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void BulkDescriptions_ChangeOnlySelectedProductsAndKeepImports()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"buildpc-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "catalogo.db");
        try
        {
            var repository = new ComponentCatalogRepository(filePath);
            repository.ReplaceImported(
                ComponentCategory.Processor,
                "kabum",
                [
                    CreateProcessor("bulk-one", "Produto um", 100m),
                    CreateProcessor("bulk-two", "Produto dois", 200m)
                ]);

            var updated = repository.UpdateDescriptions(
                ["bulk-one"],
                "Oferta especial.",
                BulkDescriptionMode.Prepend);

            Assert.Equal(1, updated);
            var products = repository.GetAll();
            var changed = products.Single(product => product.Id == "bulk-one");
            var untouched = products.Single(product => product.Id == "bulk-two");
            Assert.StartsWith("Oferta especial.", changed.Description);
            Assert.True(changed.KeepOnImport);
            Assert.Equal("Produto de teste", untouched.Description);
            Assert.False(untouched.KeepOnImport);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void DeleteMany_RemovesOnlyExplicitlySelectedProducts()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"buildpc-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "catalogo.db");
        try
        {
            var repository = new ComponentCatalogRepository(filePath);
            repository.Add(CreateManualMemory("delete-one"));
            repository.Add(CreateManualMemory("delete-two"));
            repository.Add(CreateManualMemory("keep-three"));

            var deleted = repository.DeleteMany(["delete-one", "delete-two"]);
            var remaining = new ComponentCatalogRepository(filePath).GetAll();

            Assert.Equal(2, deleted);
            Assert.DoesNotContain(remaining, product => product.Id == "delete-one");
            Assert.DoesNotContain(remaining, product => product.Id == "delete-two");
            Assert.Contains(remaining, product => product.Id == "keep-three");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void DeletedDefaultProduct_IsNotSeededAgainOnTheNextStart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"buildpc-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "catalogo.db");
        try
        {
            var repository = new ComponentCatalogRepository(filePath);
            var defaultId = ComponentCatalog.DefaultIds.First();
            Assert.Contains(repository.GetAll(), product => product.Id == defaultId);

            Assert.True(repository.Delete(defaultId));

            // Reabrir semeia o catálogo inicial; o item apagado deve continuar fora.
            var reopened = new ComponentCatalogRepository(filePath);
            Assert.DoesNotContain(reopened.GetAll(), product => product.Id == defaultId);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void DeletingImportedProducts_DoesNotLeaveMarkersBehind()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"buildpc-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "catalogo.db");
        try
        {
            var repository = new ComponentCatalogRepository(filePath);
            repository.ReplaceImported(
                ComponentCategory.Processor,
                "kabum",
                [
                    CreateProcessor("kabum-1", "Processador Um", 100m),
                    CreateProcessor("kabum-2", "Processador Dois", 200m)
                ]);

            repository.DeleteMany(["kabum-1", "kabum-2"]);

            // Marcas de exclusão só servem ao catálogo inicial; produtos
            // importados não podem deixar linhas permanentes em app_metadata.
            Assert.Equal(0, CountDeletionMarkers(filePath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ExistingDatabase_HasItsObsoleteDeletionMarkersPruned()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"buildpc-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "catalogo.db");
        try
        {
            _ = new ComponentCatalogRepository(filePath);
            var defaultMarker = $"deleted_product:{ComponentCatalog.DefaultIds.First()}";
            InsertMetadata(filePath, defaultMarker, "2026-01-01T00:00:00.0000000+00:00");
            InsertMetadata(
                filePath,
                "deleted_product:kabum-999",
                "2026-01-01T00:00:00.0000000+00:00");
            Assert.Equal(2, CountDeletionMarkers(filePath));

            _ = new ComponentCatalogRepository(filePath);

            // Sobra apenas a marca útil, do catálogo inicial.
            Assert.Equal(1, CountDeletionMarkers(filePath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void InsertMetadata(string databasePath, string key, string value)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Pooling = false
            }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT OR REPLACE INTO app_metadata(key, value) VALUES ($key, $value);";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static int CountDeletionMarkers(string databasePath)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Pooling = false
            }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM app_metadata WHERE key LIKE 'deleted_product:%';";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static PcComponent CreateProcessor(string id, string name, decimal price) =>
        new()
        {
            Id = id,
            Category = ComponentCategory.Processor,
            Name = name,
            Brand = "KaBuM!",
            Description = "Produto de teste",
            Price = price,
            PowerWatts = 0,
            Socket = "AM5",
            ImportSource = "kabum"
        };

    private static PcComponent CreateManualMemory(string id) =>
        new()
        {
            Id = id,
            Category = ComponentCategory.Memory,
            Name = $"Memória {id}",
            Brand = "Teste",
            Description = "Produto manual",
            Price = 100m
        };
}
