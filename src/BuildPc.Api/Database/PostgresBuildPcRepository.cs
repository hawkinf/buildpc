using System.Globalization;
using System.Text.Json;
using BuildPc.Core.Models;
using BuildPc.Core.Services;
using Npgsql;

namespace BuildPc.Api.Database;

public sealed class PostgresBuildPcRepository :
    IComponentCatalogRepository,
    IQuoteRepository,
    IAssemblyTemplateRepository
{
    private const string SettingsKey = "business";
    private const long QuoteNumberLock = 724_913_581;
    private const int ReplaceImportedLockNamespace = 358_112_477;
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

    // O PostgreSQL fica na mesma máquina que a API, atendendo um usuário.
    // As tarefas voltam concluídas; se a carga crescer, trocar por Npgsql
    // assíncrono é uma mudança local a esta classe.
    public Task<IReadOnlyList<PcComponent>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetAll());

    public IReadOnlyList<PcComponent> GetAll() =>
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

    public void Add(PcComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        ProductValidation.EnsureValid(component);
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

    public Task<bool> UpdateAsync(
        PcComponent component,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Update(component));

    public bool Update(PcComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        ProductValidation.EnsureValid(component);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var previous = ReadComponent(connection, transaction, component.Id);
        if (previous is null)
        {
            return false;
        }

        InsertOrUpdate(connection, transaction, component, preserveKeepFlag: false);

        // Mesmo motivo do SQLite: edição manual também é mudança de preço e
        // precisa entrar no histórico, não só importações.
        RecordPriceChange(connection, transaction, previous, component, "manual", DateTimeOffset.UtcNow);

        transaction.Commit();
        return true;
    }

    public Task<bool> DeleteAsync(
        string componentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Delete(componentId));

    public bool Delete(string componentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        return DeleteMany([componentId]) == 1;
    }

    public Task<int> DeleteManyAsync(
        IEnumerable<string> componentIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(DeleteMany(componentIds));

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

        // Um DELETE por id em loop significava milhares de round-trips numa
        // exclusão em massa da tela de Gerenciar Produtos. RETURNING id
        // devolve o que foi realmente apagado num único comando, mantendo a
        // marca de exclusão do catálogo inicial (só interessa para os ~24
        // ids padrão, então esse laço continua pequeno de propósito).
        var deletedIds = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                "DELETE FROM products WHERE lower(id) = ANY(@ids) RETURNING id;";
            command.Parameters.AddWithValue(
                "ids",
                ids.Select(id => id.ToLowerInvariant()).ToArray());
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                deletedIds.Add(reader.GetString(0));
            }
        }

        foreach (var id in deletedIds)
        {
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

        transaction.Commit();
        return deletedIds.Count;
    }

    public Task<int> UpdateDescriptionsAsync(
        IEnumerable<string> componentIds,
        string description,
        BulkDescriptionMode mode,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(UpdateDescriptions(componentIds, description, mode));

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
        // Um UPDATE por id em loop significava milhares de round-trips numa
        // edição em massa; sem efeito colateral condicional por linha (ao
        // contrário de DeleteMany), dá para trocar direto por um único
        // comando com WHERE lower(id) = ANY(@ids).
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
                WHERE lower(id) = ANY(@ids);
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
                WHERE lower(id) = ANY(@ids);
                """,
            _ =>
                """
                UPDATE products
                SET description = @description,
                    keep_on_import = CASE
                        WHEN import_source IS NULL THEN keep_on_import ELSE true
                    END
                WHERE lower(id) = ANY(@ids);
                """
        };
        command.Parameters.AddWithValue(
            "ids",
            ids.Select(id => id.ToLowerInvariant()).ToArray());
        command.Parameters.AddWithValue("description", description.Trim());
        var updated = command.ExecuteNonQuery();

        transaction.Commit();
        return updated;
    }

    public Task<ImportReplaceResult> ReplaceImportedAsync(
        ComponentCategory category,
        string source,
        IEnumerable<PcComponent> components,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ReplaceImported(category, source, components));

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

        // Sem isto, duas chamadas concorrentes para a mesma categoria/origem
        // (ex.: duplo-clique, ou duas instalações do Desktop apontando para o
        // mesmo servidor) podiam ler o mesmo "estado anterior" antes de
        // qualquer commit, perdendo um favorito ou uma linha de histórico de
        // preço. Mesmo padrão já usado para a numeração de orçamento.
        LockReplaceImported(connection, transaction, category, source);

        // Estado anterior, lido antes da substituição: alimenta o histórico de
        // preços, o aviso de itens que saíram e a preservação dos favoritos.
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
            .Count(id => !incomingIds.Contains(id));

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
            importedAt,
            priceChanges,
            disappeared);
    }

    public Task<IReadOnlyDictionary<string, DateTimeOffset>> GetLastImportsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetLastImports());

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

    public Task<bool> SetKeepOnImportAsync(
        string componentId,
        bool keep,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SetKeepOnImport(componentId, keep));

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

    public Task<BusinessSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetSettings());

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

    public Task SaveSettingsAsync(
        BusinessSettings settings,
        CancellationToken cancellationToken = default)
    {
        SaveSettings(settings);
        return Task.CompletedTask;
    }

    public void SaveSettings(BusinessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        using var connection = OpenConnection();
        SaveSettings(connection, null, settings);
    }

    public Task<SavedQuote> SaveQuoteAsync(
        SavedQuote? existing,
        QuoteDraft draft,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveQuote(existing, draft));

    public SavedQuote SaveQuote(SavedQuote? existing, QuoteDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.ClientName);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.ClientPhone);
        ArgumentNullException.ThrowIfNull(draft.Items);
        ArgumentNullException.ThrowIfNull(draft.CompanySnapshot);
        if (draft.Items.Count == 0)
        {
            throw new ArgumentException(
                "O orçamento deve possuir ao menos um item.",
                nameof(draft));
        }

        if (draft.DiscountAmount < 0m)
        {
            throw new ArgumentException(
                "O desconto não pode ser negativo.",
                nameof(draft));
        }

        QuoteValidation.EnsureMinimumMargin(draft.Items);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var totalPrice = draft.Items.Sum(item => item.TotalPrice);
        var quote = new SavedQuote
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            Number = existing?.Number ?? NextNumber(connection, transaction),
            CreatedAt = DateTimeOffset.Now,
            ClientName = draft.ClientName.Trim(),
            ClientPhone = draft.ClientPhone.Trim(),
            Notes = draft.Notes.Trim(),
            TotalCost = draft.Items.Sum(item => item.UnitCost * item.Quantity),
            TotalPrice = totalPrice,
            // Um desconto maior que o total zeraria a venda e inverteria o lucro.
            DiscountAmount = Math.Min(draft.DiscountAmount, totalPrice),
            ValidityDays = Math.Max(0, draft.ValidityDays),
            PaymentTerms = draft.PaymentTerms.Trim(),
            DeliveryTerms = draft.DeliveryTerms.Trim(),
            Items = draft.Items,
            CompanySnapshot = draft.CompanySnapshot
        };

        InsertQuote(connection, transaction, quote);
        transaction.Commit();
        return quote;
    }

    public Task<bool> DeleteQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(DeleteQuote(quoteId));

    public bool DeleteQuote(Guid quoteId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM quotes WHERE id = @id;";
        command.Parameters.AddWithValue("@id", quoteId);
        return command.ExecuteNonQuery() > 0;
    }

    public Task<IReadOnlyList<SavedQuote>> GetQuotesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetQuotes());

    public IReadOnlyList<SavedQuote> GetQuotes()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, number, created_at, client_name, client_phone, notes,
                   total_cost_cents, total_price_cents, items_json, company_json,
                   discount_cents, validity_days, payment_terms, delivery_terms
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
                    CompanySnapshot = JsonSerializer.Deserialize<CompanySnapshot>(
                                          reader.GetString(9),
                                          JsonOptions) ??
                                      new CompanySnapshot(),
                    // Orçamentos gravados antes destes campos leem zero e vazio.
                    DiscountAmount = reader.IsDBNull(10) ? 0m : reader.GetInt64(10) / 100m,
                    ValidityDays = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                    PaymentTerms = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                    DeliveryTerms = reader.IsDBNull(13) ? string.Empty : reader.GetString(13)
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
            DELETE FROM assembly_templates;
            DELETE FROM price_history;
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

        foreach (var template in snapshot.Templates)
        {
            InsertTemplate(connection, transaction, template);
        }

        transaction.Commit();
    }

    /// <summary>
    /// Grava um modelo dentro da transação da migração, sem abrir conexão nova.
    /// </summary>
    private static void InsertTemplate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AssemblyTemplate template)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO assembly_templates(
                id, name, description, created_at, items_json, kit_discount_percent)
            VALUES (@id, @name, @description, @created_at, @items_json, @kit_discount_percent)
            ON CONFLICT (id) DO UPDATE SET
                name = excluded.name,
                description = excluded.description,
                created_at = excluded.created_at,
                items_json = excluded.items_json,
                kit_discount_percent = excluded.kit_discount_percent;
            """;
        command.Parameters.AddWithValue(
            "id",
            template.Id == Guid.Empty ? Guid.NewGuid() : template.Id);
        command.Parameters.AddWithValue("name", template.Name);
        command.Parameters.AddWithValue("description", template.Description);
        command.Parameters.AddWithValue(
            "created_at",
            template.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue(
            "items_json",
            JsonSerializer.Serialize(template.Items, JsonOptions));
        command.Parameters.AddWithValue(
            "kit_discount_percent",
            template.KitDiscountPercent);
        command.ExecuteNonQuery();
    }

    public Task<bool> SetFavoriteAsync(
        string componentId,
        bool favorite,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SetFavorite(componentId, favorite));

    public bool SetFavorite(string componentId, bool favorite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE products SET is_favorite = @favorite WHERE lower(id) = lower(@id);";
        command.Parameters.AddWithValue("id", componentId);
        command.Parameters.AddWithValue("favorite", favorite);
        return command.ExecuteNonQuery() > 0;
    }

    public Task<IReadOnlyList<PriceHistoryEntry>> GetPriceHistoryAsync(
        string componentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetPriceHistory(componentId));

    public IReadOnlyList<PriceHistoryEntry> GetPriceHistory(string componentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT component_id, price_cents, recorded_at, source
            FROM price_history
            WHERE lower(component_id) = lower(@id)
            ORDER BY recorded_at DESC, id DESC;
            """;
        command.Parameters.AddWithValue("id", componentId);

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
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
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
            VALUES (@id, @price_cents, @recorded_at, @source);
            """;
        command.Parameters.AddWithValue("id", previous.Id);
        command.Parameters.AddWithValue("price_cents", ToCents(previous.Price));
        command.Parameters.AddWithValue(
            "recorded_at",
            recordedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("source", source);
        command.ExecuteNonQuery();

        return new PriceChange
        {
            ComponentId = current.Id,
            Name = current.Name,
            PreviousPrice = previous.Price,
            CurrentPrice = current.Price
        };
    }

    public Task<IReadOnlyList<AssemblyTemplate>> GetTemplatesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetTemplates());

    public IReadOnlyList<AssemblyTemplate> GetTemplates()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, description, created_at, items_json, kit_discount_percent
            FROM assembly_templates
            ORDER BY lower(name);
            """;
        using var reader = command.ExecuteReader();
        var templates = new List<AssemblyTemplate>();
        while (reader.Read())
        {
            try
            {
                templates.Add(new AssemblyTemplate
                {
                    Id = reader.GetGuid(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CreatedAt = DateTimeOffset.Parse(
                        reader.GetString(3),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind),
                    Items = JsonSerializer.Deserialize<List<AssemblyTemplateItem>>(
                                reader.GetString(4),
                                JsonOptions) ?? [],
                    KitDiscountPercent = reader.IsDBNull(5)
                        ? 0m
                        : reader.GetDecimal(5)
                });
            }
            catch (JsonException)
            {
                // Um modelo inválido não impede a leitura dos demais.
            }
        }

        return templates;
    }

    public Task<AssemblyTemplate> SaveTemplateAsync(
        AssemblyTemplate template,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveTemplate(template));

    public AssemblyTemplate SaveTemplate(AssemblyTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(template.Name);
        if (template.Items.Count == 0)
        {
            throw new ArgumentException(
                "O modelo deve possuir ao menos um item.",
                nameof(template));
        }

        if (template.KitDiscountPercent is < 0m or > 100m)
        {
            throw new ArgumentException(
                "O desconto do kit deve ficar entre 0 e 100%.",
                nameof(template));
        }

        var saved = template with
        {
            Id = template.Id == Guid.Empty ? Guid.NewGuid() : template.Id,
            Name = template.Name.Trim(),
            Description = template.Description.Trim(),
            CreatedAt = template.CreatedAt == default
                ? DateTimeOffset.Now
                : template.CreatedAt
        };

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO assembly_templates(
                id, name, description, created_at, items_json, kit_discount_percent)
            VALUES (@id, @name, @description, @created_at, @items_json, @kit_discount_percent)
            ON CONFLICT (id) DO UPDATE SET
                name = excluded.name,
                description = excluded.description,
                items_json = excluded.items_json,
                kit_discount_percent = excluded.kit_discount_percent;
            """;
        command.Parameters.AddWithValue("id", saved.Id);
        command.Parameters.AddWithValue("name", saved.Name);
        command.Parameters.AddWithValue("description", saved.Description);
        command.Parameters.AddWithValue(
            "created_at",
            saved.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue(
            "items_json",
            JsonSerializer.Serialize(saved.Items, JsonOptions));
        command.Parameters.AddWithValue(
            "kit_discount_percent",
            saved.KitDiscountPercent);
        command.ExecuteNonQuery();
        return saved;
    }

    public Task<bool> DeleteTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(DeleteTemplate(templateId));

    public bool DeleteTemplate(Guid templateId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM assembly_templates WHERE id = @id;";
        command.Parameters.AddWithValue("id", templateId);
        return command.ExecuteNonQuery() > 0;
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
            ALTER TABLE products
                ADD COLUMN IF NOT EXISTS is_favorite boolean NOT NULL DEFAULT false;
            ALTER TABLE quotes
                ADD COLUMN IF NOT EXISTS discount_cents bigint NOT NULL DEFAULT 0;
            ALTER TABLE quotes
                ADD COLUMN IF NOT EXISTS validity_days integer NOT NULL DEFAULT 0;
            ALTER TABLE quotes
                ADD COLUMN IF NOT EXISTS payment_terms text NOT NULL DEFAULT '';
            ALTER TABLE quotes
                ADD COLUMN IF NOT EXISTS delivery_terms text NOT NULL DEFAULT '';
            CREATE TABLE IF NOT EXISTS price_history (
                id bigserial PRIMARY KEY,
                component_id text NOT NULL,
                price_cents bigint NOT NULL,
                recorded_at text NOT NULL,
                source text NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_price_history_component
                ON price_history(lower(component_id), recorded_at DESC);
            CREATE TABLE IF NOT EXISTS assembly_templates (
                id uuid PRIMARY KEY,
                name text NOT NULL,
                description text NOT NULL,
                created_at text NOT NULL,
                items_json text NOT NULL
            );
            ALTER TABLE assembly_templates
                ADD COLUMN IF NOT EXISTS kit_discount_percent numeric NOT NULL DEFAULT 0;
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
        return ReadStoredComponents(connection, transaction: null);
    }

    /// <summary>
    /// Lê os produtos usando uma conexão já aberta, para participar da mesma
    /// transação de uma substituição em curso.
    /// </summary>
    private static List<PcComponent> ReadStoredComponents(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id, category, name, brand, description, price_cents, power_watts,
                   socket, memory_type, form_factor, supported_sockets,
                   supported_form_factors, import_source, keep_on_import,
                   is_user_defined, image_url, is_favorite
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
                ImageUrl = reader.IsDBNull(15) ? null : reader.GetString(15),
                IsFavorite = reader.GetBoolean(16)
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

    private static PcComponent? ReadComponent(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id, category, name, brand, description, price_cents, power_watts,
                   socket, memory_type, form_factor, supported_sockets,
                   supported_form_factors, import_source, keep_on_import,
                   is_user_defined, image_url, is_favorite
            FROM products
            WHERE lower(id) = lower(@id);
            """;
        command.Parameters.AddWithValue("id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new PcComponent
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
        };
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
                is_user_defined, image_url, is_favorite)
            VALUES (
                @id, @category, @name, @brand, @description, @price_cents,
                @power_watts, @socket, @memory_type, @form_factor,
                @supported_sockets, @supported_form_factors, @import_source,
                @keep_on_import, @is_user_defined, @image_url, @is_favorite)
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
                image_url = excluded.image_url,
                is_favorite = excluded.is_favorite;
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
        command.Parameters.AddWithValue("is_favorite", component.IsFavorite);
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

    private static void LockReplaceImported(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ComponentCategory category,
        string source)
    {
        using var lockCommand = connection.CreateCommand();
        lockCommand.Transaction = transaction;
        lockCommand.CommandText = "SELECT pg_advisory_xact_lock(@key1, @key2);";
        lockCommand.Parameters.AddWithValue("key1", ReplaceImportedLockNamespace);
        lockCommand.Parameters.AddWithValue(
            "key2",
            unchecked((int)StableHash($"{(int)category}:{source.ToLowerInvariant()}")));
        lockCommand.ExecuteNonQuery();
    }

    // String.GetHashCode() é aleatorizado por processo em .NET — não serve
    // para uma trava que precisa do mesmo valor em toda instância da API.
    private static uint StableHash(string value)
    {
        var hash = 2166136261u;
        foreach (var character in value)
        {
            hash = (hash ^ character) * 16777619u;
        }

        return hash;
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
                total_cost_cents, total_price_cents, items_json, company_json,
                discount_cents, validity_days, payment_terms, delivery_terms)
            VALUES (
                @id, @number, @created_at, @client_name, @client_phone, @notes,
                @total_cost_cents, @total_price_cents, @items_json, @company_json,
                @discount_cents, @validity_days, @payment_terms, @delivery_terms)
            ON CONFLICT (id) DO UPDATE SET
                number = excluded.number,
                created_at = excluded.created_at,
                client_name = excluded.client_name,
                client_phone = excluded.client_phone,
                notes = excluded.notes,
                total_cost_cents = excluded.total_cost_cents,
                total_price_cents = excluded.total_price_cents,
                items_json = excluded.items_json,
                company_json = excluded.company_json,
                discount_cents = excluded.discount_cents,
                validity_days = excluded.validity_days,
                payment_terms = excluded.payment_terms,
                delivery_terms = excluded.delivery_terms;
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
        command.Parameters.AddWithValue("discount_cents", ToCents(quote.DiscountAmount));
        command.Parameters.AddWithValue("validity_days", quote.ValidityDays);
        command.Parameters.AddWithValue("payment_terms", quote.PaymentTerms);
        command.Parameters.AddWithValue("delivery_terms", quote.DeliveryTerms);
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

    /// <summary>
    /// Confirma que o PostgreSQL responde de verdade, não só que o processo da
    /// API está de pé. Usado por /connection para o rodapé do Desktop não
    /// mostrar ONLINE com o banco fora do ar.
    /// </summary>
    public bool CanConnect()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1;";
        command.ExecuteScalar();
        return true;
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
