using System.Globalization;
using System.Text.Json;
using BuildPc.Core.Models;
using Microsoft.Data.Sqlite;

namespace BuildPc.Core.Services;

public sealed class QuoteRepository :
    IQuoteRepository,
    IAssemblyTemplateRepository
{
    private const string SettingsKey = "business";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;

    public QuoteRepository(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
        Initialize();
    }

    // O SQLite local responde em microssegundos; as tarefas voltam concluídas.
    public Task<BusinessSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetSettings());

    internal BusinessSettings GetSettings()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM business_settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", SettingsKey);
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

    internal void SaveSettings(BusinessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR REPLACE INTO business_settings(key, value)
            VALUES ($key, $value);
            """;
        command.Parameters.AddWithValue("$key", SettingsKey);
        command.Parameters.AddWithValue(
            "$value",
            JsonSerializer.Serialize(settings, JsonOptions));
        command.ExecuteNonQuery();
    }

    public Task<SavedQuote> SaveQuoteAsync(
        SavedQuote? existing,
        QuoteDraft draft,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveQuote(existing, draft));

    internal SavedQuote SaveQuote(SavedQuote? existing, QuoteDraft draft)
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

        // BEGIN IMMEDIATE toma a trava de escrita antes de ler MAX(number).
        // Com a transação diferida padrão, duas instâncias do programa podiam
        // ler o mesmo número e uma delas falhar na restrição UNIQUE.
        using var transaction = connection.BeginTransaction(deferred: false);
        var totalPrice = draft.Items.Sum(item => item.TotalPrice);

        // Um desconto maior que o total zeraria a venda e inverteria o lucro.
        var discount = Math.Min(draft.DiscountAmount, totalPrice);
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
            DiscountAmount = discount,
            ValidityDays = Math.Max(0, draft.ValidityDays),
            PaymentTerms = draft.PaymentTerms.Trim(),
            DeliveryTerms = draft.DeliveryTerms.Trim(),
            Items = draft.Items,
            CompanySnapshot = draft.CompanySnapshot
        };

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO quotes (
                id, number, created_at, client_name, client_phone, notes,
                total_cost_cents, total_price_cents, items_json, company_json,
                discount_cents, validity_days, payment_terms, delivery_terms)
            VALUES (
                $id, $number, $created_at, $client_name, $client_phone, $notes,
                $total_cost_cents, $total_price_cents, $items_json, $company_json,
                $discount_cents, $validity_days, $payment_terms, $delivery_terms)
            ON CONFLICT(id) DO UPDATE SET
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
        command.Parameters.AddWithValue("$id", quote.Id.ToString("D"));
        command.Parameters.AddWithValue("$number", quote.Number);
        command.Parameters.AddWithValue(
            "$created_at",
            quote.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$client_name", quote.ClientName);
        command.Parameters.AddWithValue("$client_phone", quote.ClientPhone);
        command.Parameters.AddWithValue("$notes", quote.Notes);
        command.Parameters.AddWithValue("$total_cost_cents", ToCents(quote.TotalCost));
        command.Parameters.AddWithValue("$total_price_cents", ToCents(quote.TotalPrice));
        command.Parameters.AddWithValue(
            "$items_json",
            JsonSerializer.Serialize(quote.Items, JsonOptions));
        command.Parameters.AddWithValue(
            "$company_json",
            JsonSerializer.Serialize(quote.CompanySnapshot, JsonOptions));
        command.Parameters.AddWithValue("$discount_cents", ToCents(quote.DiscountAmount));
        command.Parameters.AddWithValue("$validity_days", quote.ValidityDays);
        command.Parameters.AddWithValue("$payment_terms", quote.PaymentTerms);
        command.Parameters.AddWithValue("$delivery_terms", quote.DeliveryTerms);
        command.ExecuteNonQuery();
        transaction.Commit();
        return quote;
    }

    public Task<IReadOnlyList<SavedQuote>> GetQuotesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetQuotes());

    internal IReadOnlyList<SavedQuote> GetQuotes()
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
                    Id = Guid.Parse(reader.GetString(0)),
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
                    CompanySnapshot =
                        JsonSerializer.Deserialize<BusinessSettings>(
                            reader.GetString(9),
                            JsonOptions) ?? new BusinessSettings(),
                    // Orçamentos gravados antes destes campos existirem leem
                    // zero e vazio, e continuam válidos.
                    DiscountAmount = reader.IsDBNull(10) ? 0m : reader.GetInt64(10) / 100m,
                    ValidityDays = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                    PaymentTerms = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                    DeliveryTerms = reader.IsDBNull(13) ? string.Empty : reader.GetString(13)
                });
            }
            catch (JsonException)
            {
                // Um registro inválido não impede a leitura dos demais orçamentos.
            }
        }

        return quotes;
    }

    public Task<bool> DeleteQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(DeleteQuote(quoteId));

    internal bool DeleteQuote(Guid quoteId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM quotes WHERE id = $id;";
        command.Parameters.AddWithValue("$id", quoteId.ToString("D"));
        return command.ExecuteNonQuery() > 0;
    }

    public Task<IReadOnlyList<AssemblyTemplate>> GetTemplatesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetTemplates());

    internal IReadOnlyList<AssemblyTemplate> GetTemplates()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, description, created_at, items_json
            FROM assembly_templates
            ORDER BY name COLLATE NOCASE;
            """;
        using var reader = command.ExecuteReader();
        var templates = new List<AssemblyTemplate>();
        while (reader.Read())
        {
            try
            {
                templates.Add(new AssemblyTemplate
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CreatedAt = DateTimeOffset.Parse(
                        reader.GetString(3),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind),
                    Items = JsonSerializer.Deserialize<List<AssemblyTemplateItem>>(
                                reader.GetString(4),
                                JsonOptions) ?? []
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

    internal AssemblyTemplate SaveTemplate(AssemblyTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(template.Name);
        if (template.Items.Count == 0)
        {
            throw new ArgumentException(
                "O modelo deve possuir ao menos um item.",
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
            INSERT INTO assembly_templates(id, name, description, created_at, items_json)
            VALUES ($id, $name, $description, $created_at, $items_json)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                description = excluded.description,
                items_json = excluded.items_json;
            """;
        command.Parameters.AddWithValue("$id", saved.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", saved.Name);
        command.Parameters.AddWithValue("$description", saved.Description);
        command.Parameters.AddWithValue(
            "$created_at",
            saved.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue(
            "$items_json",
            JsonSerializer.Serialize(saved.Items, JsonOptions));
        command.ExecuteNonQuery();
        return saved;
    }

    public Task<bool> DeleteTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(DeleteTemplate(templateId));

    internal bool DeleteTemplate(Guid templateId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM assembly_templates WHERE id = $id;";
        command.Parameters.AddWithValue("$id", templateId.ToString("D"));
        return command.ExecuteNonQuery() > 0;
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS business_settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS quotes (
                id TEXT PRIMARY KEY,
                number INTEGER NOT NULL UNIQUE,
                created_at TEXT NOT NULL,
                client_name TEXT NOT NULL,
                client_phone TEXT NOT NULL,
                notes TEXT NOT NULL,
                total_cost_cents INTEGER NOT NULL,
                total_price_cents INTEGER NOT NULL,
                items_json TEXT NOT NULL,
                company_json TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_quotes_created_at
                ON quotes(created_at DESC);
            CREATE INDEX IF NOT EXISTS ix_quotes_number
                ON quotes(number DESC);
            CREATE INDEX IF NOT EXISTS ix_quotes_client_name
                ON quotes(client_name COLLATE NOCASE);
            CREATE TABLE IF NOT EXISTS assembly_templates (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL COLLATE NOCASE,
                description TEXT NOT NULL,
                created_at TEXT NOT NULL,
                items_json TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();

        EnsureQuoteColumn(connection, "discount_cents", "INTEGER NOT NULL DEFAULT 0");
        EnsureQuoteColumn(connection, "validity_days", "INTEGER NOT NULL DEFAULT 0");
        EnsureQuoteColumn(connection, "payment_terms", "TEXT NOT NULL DEFAULT ''");
        EnsureQuoteColumn(connection, "delivery_terms", "TEXT NOT NULL DEFAULT ''");
    }

    /// <summary>
    /// Acrescenta uma coluna a <c>quotes</c> se ela ainda não existir, para
    /// bases criadas por versões anteriores continuarem abrindo.
    /// </summary>
    private static void EnsureQuoteColumn(
        SqliteConnection connection,
        string columnName,
        string definition)
    {
        using (var columns = connection.CreateCommand())
        {
            columns.CommandText = "PRAGMA table_info(quotes);";
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
            $"ALTER TABLE quotes ADD COLUMN {columnName} {definition};";
        migration.ExecuteNonQuery();
    }

    private static int NextNumber(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COALESCE(MAX(number), 0) + 1 FROM quotes;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static long ToCents(decimal value) =>
        decimal.ToInt64(decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero));
}
