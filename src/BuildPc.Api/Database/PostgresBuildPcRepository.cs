using System.Globalization;
using System.Text.Json;
using BuildPc.Core.Models;
using BuildPc.Core.Services;
using Npgsql;

namespace BuildPc.Api.Database;

public sealed class PostgresBuildPcRepository :
    IComponentCatalogRepository,
    IQuoteRepository
{
    private const string SettingsKey = "business";
    private const long QuoteNumberLock = 724_913_581;
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly NpgsqlDataSource _dataSource;

    public PostgresBuildPcRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
        Initialize();
        MigrateHardDriveCategory();
        PruneUnnecessaryDeletionMarkers();
        SeedDefaultCatalog();
    }

    public IReadOnlyList<PcComponent> GetAll() =>
        ReadStoredComponents()
            .OrderBy(component => ComponentCategoryInfo.DisplayOrder(component.Category))
            .ThenBy(component => component.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public void Add(PcComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        using var connection = OpenConnection();
        if (ProductExists(connection, null, component.Id))
        {
            throw new InvalidOperationException(
                "Já existe um produto com o mesmo identificador.");
        }

        InsertOrUpdate(
            connection,
            null,
            component with
            {
                ImportSource = null,
                KeepOnImport = true,
                IsUserDefined = true
            },
            preserveKeepFlag: false);
    }

    public bool Update(PcComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        if (!ProductExists(connection, transaction, component.Id))
        {
            return false;
        }

        InsertOrUpdate(connection, transaction, component, preserveKeepFlag: false);
        transaction.Commit();
        return true;
    }

    public bool Delete(string componentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        return DeleteMany([componentId]) == 1;
    }

    public int DeleteMany(IEnumerable<string> componentIds)
    {
        ArgumentNullException.ThrowIfNull(componentIds);
        var ids = NormalizeIds(componentIds);
        if (ids.Count == 0)
        {
            return 0;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var deleted = 0;
        foreach (var id in ids)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM products WHERE lower(id) = lower(@id);";
            command.Parameters.AddWithValue("id", id);
            if (command.ExecuteNonQuery() > 0)
            {
                deleted++;

                // A marca de exclusão só impede que o catálogo inicial seja
                // semeado outra vez. Gravá-la para produtos importados fazia
                // app_metadata crescer sem limite.
                if (ComponentCatalog.DefaultIds.Contains(id))
                {
                    SetMetadata(
                        connection,
                        transaction,
                        DeletedProductKey(id),
                        DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                }
            }
        }

        transaction.Commit();
        return deleted;
    }

    public int UpdateDescriptions(
        IEnumerable<string> componentIds,
        string description,
        BulkDescriptionMode mode)
    {
        ArgumentNullException.ThrowIfNull(componentIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        var ids = NormalizeIds(componentIds);
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
                    SET description = @description || CASE
                            WHEN length(trim(description)) = 0 THEN ''
                            ELSE ' ' || description
                        END,
                        keep_on_import = CASE
                            WHEN import_source IS NULL THEN keep_on_import ELSE true
                        END
                    WHERE lower(id) = lower(@id);
                    """,
                BulkDescriptionMode.Append =>
                    """
                    UPDATE products
                    SET description = CASE
                            WHEN length(trim(description)) = 0 THEN @description
                            ELSE description || ' ' || @description
                        END,
                        keep_on_import = CASE
                            WHEN import_source IS NULL THEN keep_on_import ELSE true
                        END
                    WHERE lower(id) = lower(@id);
                    """,
                _ =>
                    """
                    UPDATE products
                    SET description = @description,
                        keep_on_import = CASE
                            WHEN import_source IS NULL THEN keep_on_import ELSE true
                        END
                    WHERE lower(id) = lower(@id);
                    """
            };
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("description", description.Trim());
            updated += command.ExecuteNonQuery();
        }

        transaction.Commit();
        return updated;
    }

    public ImportReplaceResult ReplaceImported(
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

        foreach (var component in incoming)
        {
            InsertOrUpdate(
                connection,
                transaction,
                component with
                {
                    ImportSource = source,
                    IsUserDefined = false
                },
                preserveKeepFlag: true);
        }

        var importedAt = DateTimeOffset.UtcNow;
        SetMetadata(
            connection,
            transaction,
            ImportMetadataKey(category, source),
            importedAt.ToString("O", CultureInfo.InvariantCulture));
        transaction.Commit();
        return new ImportReplaceResult(
            incoming.Count,
            removedCount,
            keptCount,
            importedAt);
    }

    public DateTimeOffset? GetLastImport(ComponentCategory category, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_metadata WHERE key = @key;";
        command.Parameters.AddWithValue("key", ImportMetadataKey(category, source));
        var value = command.ExecuteScalar() as string;
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var importedAt)
            ? importedAt
            : null;
    }

    public IReadOnlyDictionary<string, DateTimeOffset> GetLastImports()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT key, value FROM app_metadata WHERE key LIKE @prefix;";
        command.Parameters.AddWithValue("prefix", $"{ImportKeys.MetadataPrefix}%");

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

    public bool SetKeepOnImport(string componentId, bool keep)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE products
            SET keep_on_import = @keep
            WHERE lower(id) = lower(@id) AND import_source IS NOT NULL;
            """;
        command.Parameters.AddWithValue("id", componentId);
        command.Parameters.AddWithValue("keep", keep);
        return command.ExecuteNonQuery() > 0;
    }

    public BusinessSettings GetSettings()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM business_settings WHERE key = @key;";
        command.Parameters.AddWithValue("key", SettingsKey);
        var json = command.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(json))
        {
            return new BusinessSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<BusinessSettings>(json, JsonOptions) ??
                   new BusinessSettings();
        }
        catch (JsonException)
        {
            return new BusinessSettings();
        }
    }

    public void SaveSettings(BusinessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        using var connection = OpenConnection();
        SaveSettings(connection, null, settings);
    }

    public SavedQuote SaveQuote(
        SavedQuote? existing,
        string clientName,
        string clientPhone,
        string notes,
        IReadOnlyList<SavedQuoteItem> items,
        BusinessSettings companySnapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientPhone);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(companySnapshot);
        if (items.Count == 0)
        {
            throw new ArgumentException(
                "O orçamento deve possuir ao menos um item.",
                nameof(items));
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var id = existing?.Id ?? Guid.NewGuid();
        var number = existing?.Number ?? NextNumber(connection, transaction);
        var quote = new SavedQuote
        {
            Id = id,
            Number = number,
            CreatedAt = DateTimeOffset.Now,
            ClientName = clientName.Trim(),
            ClientPhone = clientPhone.Trim(),
            Notes = notes.Trim(),
            TotalCost = items.Sum(item => item.UnitCost * item.Quantity),
            TotalPrice = items.Sum(item => item.TotalPrice),
            Items = items,
            CompanySnapshot = companySnapshot
        };

        InsertQuote(connection, transaction, quote);
        transaction.Commit();
        return quote;
    }

    public bool DeleteQuote(Guid quoteId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM quotes WHERE id = @id;";
        command.Parameters.AddWithValue("@id", quoteId);
        return command.ExecuteNonQuery() > 0;
    }

    public IReadOnlyList<SavedQuote> GetQuotes()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, number, created_at, client_name, client_phone, notes,
                   total_cost_cents, total_price_cents, items_json, company_json
            FROM quotes
            ORDER BY number DESC;
            """;
        using var reader = command.ExecuteReader();
        var quotes = new List<SavedQuote>();
        while (reader.Read())
        {
            try
            {
                quotes.Add(new SavedQuote
                {
                    Id = reader.GetGuid(0),
                    Number = reader.GetInt32(1),
                    CreatedAt = DateTimeOffset.Parse(
                        reader.GetString(2),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind),
                    ClientName = reader.GetString(3),
                    ClientPhone = reader.GetString(4),
                    Notes = reader.GetString(5),
                    TotalCost = reader.GetInt64(6) / 100m,
                    TotalPrice = reader.GetInt64(7) / 100m,
                    Items = JsonSerializer.Deserialize<List<SavedQuoteItem>>(
                                reader.GetString(8),
                                JsonOptions) ?? [],
                    CompanySnapshot = JsonSerializer.Deserialize<BusinessSettings>(
                                          reader.GetString(9),
                                          JsonOptions) ??
                                      new BusinessSettings()
                });
            }
            catch (JsonException)
            {
                // Um registro inválido não impede a leitura dos demais.
            }
        }

        return quotes;
    }

    public void ImportSnapshot(BuildPcDatabaseSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        ExecuteNonQuery(
            connection,
            transaction,
            """
            DELETE FROM quotes;
            DELETE FROM business_settings;
            DELETE FROM products;
            DELETE FROM app_metadata;
            """);

        foreach (var product in snapshot.Products)
        {
            InsertOrUpdate(
                connection,
                transaction,
                product,
                preserveKeepFlag: false);
        }

        foreach (var entry in snapshot.Metadata)
        {
            SetMetadata(connection, transaction, entry.Key, entry.Value);
        }

        SaveSettings(connection, transaction, snapshot.Settings);
        foreach (var quote in snapshot.Quotes)
        {
            InsertQuote(connection, transaction, quote);
        }

        transaction.Commit();
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        ExecuteNonQuery(
            connection,
            null,
            """
            CREATE TABLE IF NOT EXISTS products (
                id text PRIMARY KEY,
                category integer NOT NULL,
                name text NOT NULL,
                brand text NOT NULL,
                description text NOT NULL,
                price_cents bigint NOT NULL,
                power_watts integer NOT NULL,
                socket text NULL,
                memory_type text NULL,
                form_factor text NULL,
                supported_sockets text NOT NULL,
                supported_form_factors text NOT NULL,
                import_source text NULL,
                keep_on_import boolean NOT NULL DEFAULT false,
                is_user_defined boolean NOT NULL DEFAULT false,
                image_url text NULL
            );
            CREATE INDEX IF NOT EXISTS ix_products_category_name
                ON products(category, lower(name));
            CREATE INDEX IF NOT EXISTS ix_products_import
                ON products(category, import_source, keep_on_import);
            CREATE TABLE IF NOT EXISTS app_metadata (
                key text PRIMARY KEY,
                value text NOT NULL
            );
            CREATE TABLE IF NOT EXISTS business_settings (
                key text PRIMARY KEY,
                value text NOT NULL
            );
            CREATE TABLE IF NOT EXISTS quotes (
                id uuid PRIMARY KEY,
                number integer NOT NULL UNIQUE,
                created_at text NOT NULL,
                client_name text NOT NULL,
                client_phone text NOT NULL,
                notes text NOT NULL,
                total_cost_cents bigint NOT NULL,
                total_price_cents bigint NOT NULL,
                items_json text NOT NULL,
                company_json text NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_quotes_created_at
                ON quotes(created_at DESC);
            CREATE INDEX IF NOT EXISTS ix_quotes_number
                ON quotes(number DESC);
            CREATE INDEX IF NOT EXISTS ix_quotes_client_name
                ON quotes(lower(client_name));
            CREATE INDEX IF NOT EXISTS ix_app_metadata_key_prefix
                ON app_metadata(key text_pattern_ops);
            """);
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
        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM app_metadata WHERE key = ANY(@keys);";
        delete.Parameters.AddWithValue("keys", obsoleteKeys.ToArray());
        delete.ExecuteNonQuery();
        transaction.Commit();
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
                SET category = @hard_drive
                WHERE category = @storage
                  AND import_source = 'kabum-hd';
                """;
            products.Parameters.AddWithValue(
                "hard_drive",
                (int)ComponentCategory.HardDrive);
            products.Parameters.AddWithValue("storage", (int)ComponentCategory.Storage);
            products.ExecuteNonQuery();
        }

        using (var metadata = connection.CreateCommand())
        {
            metadata.Transaction = transaction;
            metadata.CommandText =
                """
                INSERT INTO app_metadata(key, value)
                SELECT @new_key, value
                FROM app_metadata
                WHERE key = @old_key
                ON CONFLICT (key) DO NOTHING;
                """;
            metadata.Parameters.AddWithValue(
                "new_key",
                ImportMetadataKey(ComponentCategory.HardDrive, "kabum-hd"));
            metadata.Parameters.AddWithValue(
                "old_key",
                ImportMetadataKey(ComponentCategory.Storage, "kabum-hd"));
            metadata.ExecuteNonQuery();
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
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, category, name, brand, description, price_cents, power_watts,
                   socket, memory_type, form_factor, supported_sockets,
                   supported_form_factors, import_source, keep_on_import,
                   is_user_defined, image_url
            FROM products
            ORDER BY category, lower(name);
            """;
        using var reader = command.ExecuteReader();
        var products = new List<PcComponent>();
        while (reader.Read())
        {
            products.Add(new PcComponent
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
                ImageUrl = reader.IsDBNull(15) ? null : reader.GetString(15)
            });
        }

        return products;
    }

    private static List<string> NormalizeIds(IEnumerable<string> componentIds) =>
        componentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool ProductExists(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT EXISTS(SELECT 1 FROM products WHERE lower(id) = lower(@id));";
        command.Parameters.AddWithValue("id", id);
        return Convert.ToBoolean(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static bool HasMetadata(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string key)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT EXISTS(SELECT 1 FROM app_metadata WHERE key = @key);";
        command.Parameters.AddWithValue("key", key);
        return Convert.ToBoolean(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static int CountImported(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
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
            WHERE category = @category
              AND import_source = @source
              AND (@keep_only = false OR keep_on_import = true);
            """;
        command.Parameters.AddWithValue("category", (int)category);
        command.Parameters.AddWithValue("source", source);
        command.Parameters.AddWithValue("keep_only", keepOnly);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static int DeleteReplaceableImported(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ComponentCategory category,
        string source)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            DELETE FROM products
            WHERE category = @category
              AND import_source = @source
              AND keep_on_import = false;
            """;
        command.Parameters.AddWithValue("category", (int)category);
        command.Parameters.AddWithValue("source", source);
        return command.ExecuteNonQuery();
    }

    private static void InsertOrUpdate(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        PcComponent component,
        bool preserveKeepFlag)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO products (
                id, category, name, brand, description, price_cents, power_watts,
                socket, memory_type, form_factor, supported_sockets,
                supported_form_factors, import_source, keep_on_import,
                is_user_defined, image_url)
            VALUES (
                @id, @category, @name, @brand, @description, @price_cents,
                @power_watts, @socket, @memory_type, @form_factor,
                @supported_sockets, @supported_form_factors, @import_source,
                @keep_on_import, @is_user_defined, @image_url)
            ON CONFLICT (id) DO UPDATE SET
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
                keep_on_import = CASE
                    WHEN @preserve_keep THEN products.keep_on_import
                    ELSE excluded.keep_on_import
                END,
                is_user_defined = excluded.is_user_defined,
                image_url = excluded.image_url;
            """;
        command.Parameters.AddWithValue("id", component.Id);
        command.Parameters.AddWithValue("category", (int)component.Category);
        command.Parameters.AddWithValue("name", component.Name);
        command.Parameters.AddWithValue("brand", component.Brand);
        command.Parameters.AddWithValue("description", component.Description);
        command.Parameters.AddWithValue("price_cents", ToCents(component.Price));
        command.Parameters.AddWithValue("power_watts", component.PowerWatts);
        command.Parameters.AddWithValue("socket", (object?)component.Socket ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "memory_type",
            (object?)component.MemoryType ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "form_factor",
            (object?)component.FormFactor ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "supported_sockets",
            SerializeSet(component.SupportedSockets));
        command.Parameters.AddWithValue(
            "supported_form_factors",
            SerializeSet(component.SupportedFormFactors));
        command.Parameters.AddWithValue(
            "import_source",
            (object?)component.ImportSource ?? DBNull.Value);
        command.Parameters.AddWithValue("keep_on_import", component.KeepOnImport);
        command.Parameters.AddWithValue("is_user_defined", component.IsUserDefined);
        command.Parameters.AddWithValue(
            "image_url",
            (object?)component.ImageUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("preserve_keep", preserveKeepFlag);
        command.ExecuteNonQuery();
    }

    private static void SetMetadata(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string key,
        string value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO app_metadata(key, value)
            VALUES (@key, @value)
            ON CONFLICT (key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("value", value);
        command.ExecuteNonQuery();
    }

    private static void SaveSettings(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        BusinessSettings settings)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO business_settings(key, value)
            VALUES (@key, @value)
            ON CONFLICT (key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("key", SettingsKey);
        command.Parameters.AddWithValue(
            "value",
            JsonSerializer.Serialize(settings, JsonOptions));
        command.ExecuteNonQuery();
    }

    private static int NextNumber(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = "SELECT pg_advisory_xact_lock(@lock_id);";
            lockCommand.Parameters.AddWithValue("lock_id", QuoteNumberLock);
            lockCommand.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COALESCE(MAX(number), 0) + 1 FROM quotes;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void InsertQuote(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        SavedQuote quote)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO quotes (
                id, number, created_at, client_name, client_phone, notes,
                total_cost_cents, total_price_cents, items_json, company_json)
            VALUES (
                @id, @number, @created_at, @client_name, @client_phone, @notes,
                @total_cost_cents, @total_price_cents, @items_json, @company_json)
            ON CONFLICT (id) DO UPDATE SET
                number = excluded.number,
                created_at = excluded.created_at,
                client_name = excluded.client_name,
                client_phone = excluded.client_phone,
                notes = excluded.notes,
                total_cost_cents = excluded.total_cost_cents,
                total_price_cents = excluded.total_price_cents,
                items_json = excluded.items_json,
                company_json = excluded.company_json;
            """;
        command.Parameters.AddWithValue("id", quote.Id);
        command.Parameters.AddWithValue("number", quote.Number);
        command.Parameters.AddWithValue(
            "created_at",
            quote.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("client_name", quote.ClientName);
        command.Parameters.AddWithValue("client_phone", quote.ClientPhone);
        command.Parameters.AddWithValue("notes", quote.Notes);
        command.Parameters.AddWithValue(
            "total_cost_cents",
            ToCents(quote.TotalCost));
        command.Parameters.AddWithValue(
            "total_price_cents",
            ToCents(quote.TotalPrice));
        command.Parameters.AddWithValue(
            "items_json",
            JsonSerializer.Serialize(quote.Items, JsonOptions));
        command.Parameters.AddWithValue(
            "company_json",
            JsonSerializer.Serialize(quote.CompanySnapshot, JsonOptions));
        command.ExecuteNonQuery();
    }

    private static void ExecuteNonQuery(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private NpgsqlConnection OpenConnection() =>
        _dataSource.OpenConnection();

    private static long ToCents(decimal value) =>
        decimal.ToInt64(
            decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero));

    private static string SerializeSet(IEnumerable<string> values) =>
        JsonSerializer.Serialize(values);

    private static HashSet<string> DeserializeSet(string json) =>
        new(
            JsonSerializer.Deserialize<List<string>>(json) ?? [],
            StringComparer.OrdinalIgnoreCase);

    private static string ImportMetadataKey(
        ComponentCategory category,
        string source) =>
        ImportKeys.MetadataKey(category, source);

    private const string DeletedProductKeyPrefix = "deleted_product:";

    private static string DeletedProductKey(string componentId) =>
        $"{DeletedProductKeyPrefix}{componentId.Trim().ToLowerInvariant()}";
}
