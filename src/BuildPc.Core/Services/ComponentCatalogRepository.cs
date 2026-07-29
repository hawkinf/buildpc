using System.Text.Json;
using System.Globalization;
using BuildPc.Core.Models;
using Microsoft.Data.Sqlite;

namespace BuildPc.Core.Services;

public sealed class ComponentCatalogRepository : IComponentCatalogRepository
{
    private readonly string _connectionString;

    public ComponentCatalogRepository(string databasePath, string? legacyJsonPath = null)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        InitializeDatabase();
        MigrateHardDriveCategory();
        PruneUnnecessaryDeletionMarkers();
        SeedDefaultCatalog();
        MigrateLegacyJsonOnce(legacyJsonPath);
    }

    // O SQLite local responde em microssegundos; envolver em Task.Run só
    // acrescentaria troca de thread. As tarefas voltam já concluídas.
    public Task<IReadOnlyList<PcComponent>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetAll());

    internal IReadOnlyList<PcComponent> GetAll() =>
        ReadStoredComponents()
            .OrderBy(component => ComponentCategoryInfo.DisplayOrder(component.Category))
            .ThenBy(component => component.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public Task AddAsync(
        PcComponent component,
        CancellationToken cancellationToken = default)
    {
        Add(component);
        return Task.CompletedTask;
    }

    internal void Add(PcComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM products WHERE id = $id;";
        command.Parameters.AddWithValue("$id", component.Id);
        if (Convert.ToInt32(command.ExecuteScalar()) > 0)
        {
            throw new InvalidOperationException("Já existe um produto com o mesmo identificador.");
        }

        InsertOrUpdate(
            connection,
            transaction: null,
            component with
            {
                ImportSource = null,
                KeepOnImport = true,
                IsUserDefined = true
            },
            preserveKeepFlag: false);
    }

    public Task<bool> UpdateAsync(
        PcComponent component,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Update(component));

    internal bool Update(PcComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        if (!ProductExists(connection, transaction, component.Id))
        {
            return false;
        }

        InsertOrUpdate(
            connection,
            transaction,
            component,
            preserveKeepFlag: false);
        transaction.Commit();
        return true;
    }

    public Task<bool> DeleteAsync(
        string componentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Delete(componentId));

    internal bool Delete(string componentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        return DeleteMany([componentId]) == 1;
    }

    public Task<int> DeleteManyAsync(
        IEnumerable<string> componentIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(DeleteMany(componentIds));

    internal int DeleteMany(IEnumerable<string> componentIds)
    {
        ArgumentNullException.ThrowIfNull(componentIds);
        var ids = componentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var deleted = 0;
        foreach (var componentId in ids)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM products WHERE id = $id;";
            command.Parameters.AddWithValue("$id", componentId);
            if (command.ExecuteNonQuery() > 0)
            {
                deleted++;

                // A marca de exclusão só existe para impedir que o catálogo
                // inicial seja semeado outra vez. Gravá-la para produtos
                // importados fazia app_metadata crescer sem limite: milhares de
                // linhas permanentes a cada limpeza de importação.
                if (ComponentCatalog.DefaultIds.Contains(componentId))
                {
                    SetMetadata(
                        connection,
                        transaction,
                        DeletedProductKey(componentId),
                        DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                }
            }
        }

        transaction.Commit();
        return deleted;
    }

    public Task<int> UpdateDescriptionsAsync(
        IEnumerable<string> componentIds,
        string description,
        BulkDescriptionMode mode,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(UpdateDescriptions(componentIds, description, mode));

    internal int UpdateDescriptions(
        IEnumerable<string> componentIds,
        string description,
        BulkDescriptionMode mode)
    {
        ArgumentNullException.ThrowIfNull(componentIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        var ids = componentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var updated = 0;
        foreach (var id in ids)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = mode switch
            {
                BulkDescriptionMode.Prepend =>
                    """
                    UPDATE products
                    SET description = $description || CASE
                            WHEN length(trim(description)) = 0 THEN ''
                            ELSE ' ' || description
                        END,
                        keep_on_import = CASE
                            WHEN import_source IS NULL THEN keep_on_import ELSE 1
                        END
                    WHERE id = $id;
                    """,
                BulkDescriptionMode.Append =>
                    """
                    UPDATE products
                    SET description = CASE
                            WHEN length(trim(description)) = 0 THEN $description
                            ELSE description || ' ' || $description
                        END,
                        keep_on_import = CASE
                            WHEN import_source IS NULL THEN keep_on_import ELSE 1
                        END
                    WHERE id = $id;
                    """,
                _ =>
                    """
                    UPDATE products
                    SET description = $description,
                        keep_on_import = CASE
                            WHEN import_source IS NULL THEN keep_on_import ELSE 1
                        END
                    WHERE id = $id;
                    """
            };
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$description", description.Trim());
            updated += command.ExecuteNonQuery();
        }

        transaction.Commit();
        return updated;
    }

    public Task<ImportReplaceResult> ReplaceImportedAsync(
        ComponentCategory category,
        string source,
        IEnumerable<PcComponent> components,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ReplaceImported(category, source, components));

    internal ImportReplaceResult ReplaceImported(
        ComponentCategory category,
        string source,
        IEnumerable<PcComponent> components)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(components);

        var incoming = components
            .Where(component => component.Category == category)
            .GroupBy(component => component.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        // Preços antes da substituição, para detectar variações e para o
        // aviso de produtos que deixaram de vir na carga.
        var previousByCategory = ReadStoredComponents(connection, transaction)
            .Where(component =>
                component.Category == category &&
                string.Equals(
                    component.ImportSource,
                    source,
                    StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                component => component.Id,
                component => component,
                StringComparer.OrdinalIgnoreCase);

        var keptCount = CountImported(
            connection,
            transaction,
            category,
            source,
            keepOnly: true);
        var removedCount = DeleteReplaceableImported(
            connection,
            transaction,
            category,
            source);

        var importedAt = DateTimeOffset.UtcNow;
        var priceChanges = new List<PriceChange>();
        foreach (var component in incoming)
        {
            previousByCategory.TryGetValue(component.Id, out var previous);
            if (previous is not null &&
                RecordPriceChange(
                    connection,
                    transaction,
                    previous,
                    component,
                    source,
                    importedAt) is { } change)
            {
                priceChanges.Add(change);
            }

            // DeleteReplaceableImported já removeu a linha anterior, então o
            // ON CONFLICT não tem com o que preservar: o favorito precisa ser
            // reaplicado a partir do estado lido antes da substituição.
            InsertOrUpdate(
                connection,
                transaction,
                component with
                {
                    ImportSource = source,
                    IsUserDefined = false,
                    IsFavorite = previous?.IsFavorite ?? component.IsFavorite
                },
                preserveKeepFlag: true);
        }

        var incomingIds = incoming
            .Select(component => component.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var disappeared = previousByCategory.Keys
            .Where(id => !incomingIds.Contains(id))
            .Count();

        SetMetadata(
            connection,
            transaction,
            ImportMetadataKey(category, source),
            importedAt.ToString("O", CultureInfo.InvariantCulture));
        transaction.Commit();
        return new(
            incoming.Count,
            removedCount,
            keptCount,
            importedAt,
            priceChanges,
            disappeared);
    }

    internal DateTimeOffset? GetLastImport(ComponentCategory category, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_metadata WHERE key = $key;";
        command.Parameters.AddWithValue("$key", ImportMetadataKey(category, source));
        var value = command.ExecuteScalar() as string;

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var importedAt)
            ? importedAt
            : null;
    }

    public Task<IReadOnlyDictionary<string, DateTimeOffset>> GetLastImportsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetLastImports());

    internal IReadOnlyDictionary<string, DateTimeOffset> GetLastImports()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT key, value FROM app_metadata WHERE key LIKE $prefix;";
        command.Parameters.AddWithValue("$prefix", $"{ImportKeys.MetadataPrefix}%");

        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, DateTimeOffset>(
            StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            if (DateTimeOffset.TryParse(
                    reader.GetString(1),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var importedAt))
            {
                result[reader.GetString(0)[ImportKeys.MetadataPrefix.Length..]] =
                    importedAt;
            }
        }

        return result;
    }

    public Task<bool> SetKeepOnImportAsync(
        string componentId,
        bool keep,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SetKeepOnImport(componentId, keep));

    internal bool SetKeepOnImport(string componentId, bool keep)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE products
            SET keep_on_import = $keep
            WHERE id = $id AND import_source IS NOT NULL;
            """;
        command.Parameters.AddWithValue("$id", componentId);
        command.Parameters.AddWithValue("$keep", keep ? 1 : 0);
        return command.ExecuteNonQuery() > 0;
    }

    public Task<bool> SetFavoriteAsync(
        string componentId,
        bool favorite,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SetFavorite(componentId, favorite));

    internal bool SetFavorite(string componentId, bool favorite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE products SET is_favorite = $favorite WHERE id = $id;";
        command.Parameters.AddWithValue("$id", componentId);
        command.Parameters.AddWithValue("$favorite", favorite ? 1 : 0);
        return command.ExecuteNonQuery() > 0;
    }

    public Task<IReadOnlyList<PriceHistoryEntry>> GetPriceHistoryAsync(
        string componentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetPriceHistory(componentId));

    internal IReadOnlyList<PriceHistoryEntry> GetPriceHistory(string componentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT component_id, price_cents, recorded_at, source
            FROM price_history
            WHERE component_id = $id
            ORDER BY recorded_at DESC, id DESC;
            """;
        command.Parameters.AddWithValue("$id", componentId);

        using var reader = command.ExecuteReader();
        var entries = new List<PriceHistoryEntry>();
        while (reader.Read())
        {
            entries.Add(new PriceHistoryEntry
            {
                ComponentId = reader.GetString(0),
                Price = reader.GetInt64(1) / 100m,
                RecordedAt = DateTimeOffset.Parse(
                    reader.GetString(2),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                Source = reader.GetString(3)
            });
        }

        return entries;
    }

    /// <summary>
    /// Grava o custo anterior de um produto quando ele muda, e devolve a
    /// variação. Só registra mudanças reais, para o histórico não crescer com
    /// uma linha por importação sem alteração de preço.
    /// </summary>
    private static PriceChange? RecordPriceChange(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PcComponent previous,
        PcComponent current,
        string source,
        DateTimeOffset recordedAt)
    {
        if (previous.Price == current.Price)
        {
            return null;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO price_history(component_id, price_cents, recorded_at, source)
            VALUES ($id, $price_cents, $recorded_at, $source);
            """;
        command.Parameters.AddWithValue("$id", previous.Id);
        command.Parameters.AddWithValue("$price_cents", DecimalToCents(previous.Price));
        command.Parameters.AddWithValue(
            "$recorded_at",
            recordedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$source", source);
        command.ExecuteNonQuery();

        return new PriceChange
        {
            ComponentId = current.Id,
            Name = current.Name,
            PreviousPrice = previous.Price,
            CurrentPrice = current.Price
        };
    }

    private void InitializeDatabase()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS products (
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
                is_user_defined INTEGER NOT NULL DEFAULT 0,
                image_url TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_products_category_name
                ON products(category, name COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS ix_products_import
                ON products(category, import_source, keep_on_import);
            CREATE TABLE IF NOT EXISTS app_metadata (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS price_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                component_id TEXT NOT NULL COLLATE NOCASE,
                price_cents INTEGER NOT NULL,
                recorded_at TEXT NOT NULL,
                source TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_price_history_component
                ON price_history(component_id COLLATE NOCASE, recorded_at DESC);
            CREATE TABLE IF NOT EXISTS assembly_templates (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL COLLATE NOCASE,
                description TEXT NOT NULL,
                created_at TEXT NOT NULL,
                items_json TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        EnsureColumn(connection, "image_url", "TEXT NULL");
        EnsureColumn(connection, "is_favorite", "INTEGER NOT NULL DEFAULT 0");
    }

    /// <summary>
    /// Acrescenta uma coluna a <c>products</c> se ela ainda não existir, para
    /// bases criadas por versões anteriores continuarem abrindo.
    /// </summary>
    private static void EnsureColumn(
        SqliteConnection connection,
        string columnName,
        string definition)
    {
        using (var columns = connection.CreateCommand())
        {
            columns.CommandText = "PRAGMA table_info(products);";
            using var reader = columns.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(
                        reader.GetString(1),
                        columnName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        using var migration = connection.CreateCommand();
        migration.CommandText =
            $"ALTER TABLE products ADD COLUMN {columnName} {definition};";
        migration.ExecuteNonQuery();
    }

    private void MigrateHardDriveCategory()
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var products = connection.CreateCommand())
        {
            products.Transaction = transaction;
            products.CommandText =
                """
                UPDATE products
                SET category = $hard_drive
                WHERE category = $storage
                  AND import_source = 'kabum-hd';
                """;
            products.Parameters.AddWithValue("$hard_drive", (int)ComponentCategory.HardDrive);
            products.Parameters.AddWithValue("$storage", (int)ComponentCategory.Storage);
            products.ExecuteNonQuery();
        }

        using (var metadata = connection.CreateCommand())
        {
            metadata.Transaction = transaction;
            metadata.CommandText =
                """
                INSERT OR IGNORE INTO app_metadata(key, value)
                SELECT $new_key, value
                FROM app_metadata
                WHERE key = $old_key;
                """;
            metadata.Parameters.AddWithValue(
                "$new_key",
                ImportMetadataKey(ComponentCategory.HardDrive, "kabum-hd"));
            metadata.Parameters.AddWithValue(
                "$old_key",
                ImportMetadataKey(ComponentCategory.Storage, "kabum-hd"));
            metadata.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <summary>
    /// Remove marcas de exclusão que não pertencem ao catálogo inicial. Bases
    /// criadas antes desta correção acumularam uma linha permanente por produto
    /// importado que já foi apagado.
    /// </summary>
    private void PruneUnnecessaryDeletionMarkers()
    {
        using var connection = OpenConnection();
        var obsoleteKeys = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT key FROM app_metadata WHERE key LIKE 'deleted_product:%';";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var key = reader.GetString(0);
                if (!ComponentCatalog.DefaultIds.Contains(
                        key[DeletedProductKeyPrefix.Length..]))
                {
                    obsoleteKeys.Add(key);
                }
            }
        }

        if (obsoleteKeys.Count == 0)
        {
            return;
        }

        using var transaction = connection.BeginTransaction();
        foreach (var key in obsoleteKeys)
        {
            using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM app_metadata WHERE key = $key;";
            delete.Parameters.AddWithValue("$key", key);
            delete.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private void SeedDefaultCatalog()
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var component in ComponentCatalog.CreateDefault())
        {
            if (ProductExists(connection, transaction, component.Id) ||
                HasMetadata(connection, transaction, DeletedProductKey(component.Id)))
            {
                continue;
            }

            InsertOrUpdate(
                connection,
                transaction,
                component with
                {
                    ImportSource = null,
                    KeepOnImport = false,
                    IsUserDefined = false
                },
                preserveKeepFlag: false);
        }

        transaction.Commit();
    }

    private List<PcComponent> ReadStoredComponents()
    {
        using var connection = OpenConnection();
        return ReadStoredComponents(connection, transaction: null);
    }

    /// <summary>
    /// Lê os produtos usando uma conexão já aberta, para participar da mesma
    /// transação de uma substituição em curso.
    /// </summary>
    private static List<PcComponent> ReadStoredComponents(
        SqliteConnection connection,
        SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id, category, name, brand, description, price_cents, power_watts,
                   socket, memory_type, form_factor, supported_sockets,
                   supported_form_factors, import_source, keep_on_import, is_user_defined,
                   image_url, is_favorite
            FROM products
            ORDER BY category, name COLLATE NOCASE;
            """;

        using var reader = command.ExecuteReader();
        var components = new List<PcComponent>();
        while (reader.Read())
        {
            components.Add(new PcComponent
            {
                Id = reader.GetString(0),
                Category = (ComponentCategory)reader.GetInt32(1),
                Name = reader.GetString(2),
                Brand = reader.GetString(3),
                Description = reader.GetString(4),
                Price = reader.GetInt64(5) / 100m,
                PowerWatts = reader.GetInt32(6),
                Socket = reader.IsDBNull(7) ? null : reader.GetString(7),
                MemoryType = reader.IsDBNull(8) ? null : reader.GetString(8),
                FormFactor = reader.IsDBNull(9) ? null : reader.GetString(9),
                SupportedSockets = DeserializeSet(reader.GetString(10)),
                SupportedFormFactors = DeserializeSet(reader.GetString(11)),
                ImportSource = reader.IsDBNull(12) ? null : reader.GetString(12),
                KeepOnImport = reader.GetBoolean(13),
                IsUserDefined = reader.GetBoolean(14),
                ImageUrl = reader.IsDBNull(15) ? null : reader.GetString(15),
                IsFavorite = reader.GetBoolean(16)
            });
        }

        return components;
    }

    private void MigrateLegacyJsonOnce(string? legacyJsonPath)
    {
        using var connection = OpenConnection();
        if (HasMigrationMarker(connection))
        {
            return;
        }

        using var transaction = connection.BeginTransaction();
        if (!string.IsNullOrWhiteSpace(legacyJsonPath) && File.Exists(legacyJsonPath))
        {
            try
            {
                var json = File.ReadAllText(legacyJsonPath);
                var legacyComponents = JsonSerializer.Deserialize<List<PcComponent>>(json) ?? [];
                foreach (var component in legacyComponents)
                {
                    var imported = component.Id.StartsWith(
                        "kabum-",
                        StringComparison.OrdinalIgnoreCase);
                    InsertOrUpdate(
                        connection,
                        transaction,
                        component with
                        {
                            ImportSource = imported ? "kabum" : null,
                            KeepOnImport = !imported,
                            IsUserDefined = !imported
                        },
                        preserveKeepFlag: false);
                }
            }
            catch (JsonException)
            {
                // O arquivo antigo é ignorado; o banco continua utilizável.
            }
        }

        using var marker = connection.CreateCommand();
        marker.Transaction = transaction;
        marker.CommandText =
            "INSERT OR REPLACE INTO app_metadata(key, value) VALUES ('legacy_json_migrated', '1');";
        marker.ExecuteNonQuery();
        transaction.Commit();
    }

    private static bool HasMigrationMarker(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM app_metadata WHERE key = 'legacy_json_migrated';";
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool ProductExists(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM products WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool HasMetadata(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string key)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM app_metadata WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static int CountImported(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ComponentCategory category,
        string source,
        bool keepOnly)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM products
            WHERE category = $category
              AND import_source = $source
              AND ($keep_only = 0 OR keep_on_import = 1);
            """;
        command.Parameters.AddWithValue("$category", (int)category);
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$keep_only", keepOnly ? 1 : 0);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static int DeleteReplaceableImported(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ComponentCategory category,
        string source)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            DELETE FROM products
            WHERE category = $category
              AND import_source = $source
              AND keep_on_import = 0;
            """;
        command.Parameters.AddWithValue("$category", (int)category);
        command.Parameters.AddWithValue("$source", source);
        return command.ExecuteNonQuery();
    }

    private static void SetMetadata(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string key,
        string value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT OR REPLACE INTO app_metadata(key, value) VALUES ($key, $value);";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static string ImportMetadataKey(ComponentCategory category, string source) =>
        ImportKeys.MetadataKey(category, source);

    private const string DeletedProductKeyPrefix = "deleted_product:";

    private static string DeletedProductKey(string componentId) =>
        $"{DeletedProductKeyPrefix}{componentId.Trim().ToLowerInvariant()}";

    private static void InsertOrUpdate(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        PcComponent component,
        bool preserveKeepFlag)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            preserveKeepFlag
                ?
                """
                INSERT INTO products (
                    id, category, name, brand, description, price_cents, power_watts,
                    socket, memory_type, form_factor, supported_sockets,
                    supported_form_factors, import_source, keep_on_import, is_user_defined,
                    image_url, is_favorite)
                VALUES (
                    $id, $category, $name, $brand, $description, $price_cents, $power_watts,
                    $socket, $memory_type, $form_factor, $supported_sockets,
                    $supported_form_factors, $import_source, $keep_on_import, $is_user_defined,
                    $image_url, $is_favorite)
                ON CONFLICT(id) DO UPDATE SET
                    category = excluded.category,
                    name = excluded.name,
                    brand = excluded.brand,
                    description = excluded.description,
                    price_cents = excluded.price_cents,
                    power_watts = excluded.power_watts,
                    socket = excluded.socket,
                    memory_type = excluded.memory_type,
                    form_factor = excluded.form_factor,
                    supported_sockets = excluded.supported_sockets,
                    supported_form_factors = excluded.supported_form_factors,
                    import_source = excluded.import_source,
                    keep_on_import = products.keep_on_import,
                    is_user_defined = excluded.is_user_defined,
                    image_url = excluded.image_url,
                    -- Favorito é escolha do usuário: a importação não desfaz.
                    is_favorite = products.is_favorite;
                """
                :
                """
                INSERT OR REPLACE INTO products (
                    id, category, name, brand, description, price_cents, power_watts,
                    socket, memory_type, form_factor, supported_sockets,
                    supported_form_factors, import_source, keep_on_import, is_user_defined,
                    image_url, is_favorite)
                VALUES (
                    $id, $category, $name, $brand, $description, $price_cents, $power_watts,
                    $socket, $memory_type, $form_factor, $supported_sockets,
                    $supported_form_factors, $import_source, $keep_on_import, $is_user_defined,
                    $image_url, $is_favorite);
                """;

        command.Parameters.AddWithValue("$id", component.Id);
        command.Parameters.AddWithValue("$category", (int)component.Category);
        command.Parameters.AddWithValue("$name", component.Name);
        command.Parameters.AddWithValue("$brand", component.Brand);
        command.Parameters.AddWithValue("$description", component.Description);
        command.Parameters.AddWithValue("$price_cents", DecimalToCents(component.Price));
        command.Parameters.AddWithValue("$power_watts", component.PowerWatts);
        command.Parameters.AddWithValue("$socket", (object?)component.Socket ?? DBNull.Value);
        command.Parameters.AddWithValue("$memory_type", (object?)component.MemoryType ?? DBNull.Value);
        command.Parameters.AddWithValue("$form_factor", (object?)component.FormFactor ?? DBNull.Value);
        command.Parameters.AddWithValue("$supported_sockets", SerializeSet(component.SupportedSockets));
        command.Parameters.AddWithValue(
            "$supported_form_factors",
            SerializeSet(component.SupportedFormFactors));
        command.Parameters.AddWithValue(
            "$import_source",
            (object?)component.ImportSource ?? DBNull.Value);
        command.Parameters.AddWithValue("$keep_on_import", component.KeepOnImport ? 1 : 0);
        command.Parameters.AddWithValue("$is_user_defined", component.IsUserDefined ? 1 : 0);
        command.Parameters.AddWithValue(
            "$image_url",
            (object?)component.ImageUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$is_favorite", component.IsFavorite ? 1 : 0);
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static long DecimalToCents(decimal price) =>
        decimal.ToInt64(decimal.Round(price * 100m, 0, MidpointRounding.AwayFromZero));

    private static string SerializeSet(IEnumerable<string> values) =>
        JsonSerializer.Serialize(values);

    private static HashSet<string> DeserializeSet(string json) =>
        new(
            JsonSerializer.Deserialize<List<string>>(json) ?? [],
            StringComparer.OrdinalIgnoreCase);
}

public sealed record ImportReplaceResult(
    int Imported,
    int Removed,
    int Kept,
    DateTimeOffset ImportedAt,
    /// <summary>Produtos cujo custo mudou em relação à carga anterior.</summary>
    IReadOnlyList<PriceChange> PriceChanges,
    /// <summary>
    /// Produtos que existiam na carga anterior e não voltaram nesta. Costuma
    /// indicar item esgotado ou saído de linha na loja.
    /// </summary>
    int Disappeared)
{
    /// <summary>Sobrecarga para chamadas que não acompanham variações.</summary>
    public ImportReplaceResult(
        int imported,
        int removed,
        int kept,
        DateTimeOffset importedAt)
        : this(imported, removed, kept, importedAt, [], 0)
    {
    }

    public bool HasPriceChanges => PriceChanges.Count > 0;

    public int Increases => PriceChanges.Count(change => change.IsIncrease);

    public int Decreases => PriceChanges.Count(change => !change.IsIncrease);
}
