using System.Globalization;
using System.Text.Json;
using BuildPc.Core.Models;
using Microsoft.Data.Sqlite;

namespace BuildPc.Core.Services;

public sealed class QuoteRepository : IQuoteRepository
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

    public BusinessSettings GetSettings()
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

    public void SaveSettings(BusinessSettings settings)
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
            throw new ArgumentException("O orçamento deve possuir ao menos um item.", nameof(items));
        }

        using var connection = OpenConnection();

        // BEGIN IMMEDIATE toma a trava de escrita antes de ler MAX(number).
        // Com a transação diferida padrão, duas instâncias do programa podiam
        // ler o mesmo número e uma delas falhar na restrição UNIQUE.
        using var transaction = connection.BeginTransaction(deferred: false);
        var id = existing?.Id ?? Guid.NewGuid();
        var number = existing?.Number ?? NextNumber(connection, transaction);
        var createdAt = DateTimeOffset.Now;
        var totalCost = items.Sum(item => item.UnitCost * item.Quantity);
        var totalPrice = items.Sum(item => item.TotalPrice);
        var quote = new SavedQuote
        {
            Id = id,
            Number = number,
            CreatedAt = createdAt,
            ClientName = clientName.Trim(),
            ClientPhone = clientPhone.Trim(),
            Notes = notes.Trim(),
            TotalCost = totalCost,
            TotalPrice = totalPrice,
            Items = items,
            CompanySnapshot = companySnapshot
        };

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO quotes (
                id, number, created_at, client_name, client_phone, notes,
                total_cost_cents, total_price_cents, items_json, company_json)
            VALUES (
                $id, $number, $created_at, $client_name, $client_phone, $notes,
                $total_cost_cents, $total_price_cents, $items_json, $company_json)
            ON CONFLICT(id) DO UPDATE SET
                created_at = excluded.created_at,
                client_name = excluded.client_name,
                client_phone = excluded.client_phone,
                notes = excluded.notes,
                total_cost_cents = excluded.total_cost_cents,
                total_price_cents = excluded.total_price_cents,
                items_json = excluded.items_json,
                company_json = excluded.company_json;
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
        command.ExecuteNonQuery();
        transaction.Commit();
        return quote;
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
                            JsonOptions) ?? new BusinessSettings()
                });
            }
            catch (JsonException)
            {
                // Um registro inválido não impede a leitura dos demais orçamentos.
            }
        }

        return quotes;
    }

    public bool DeleteQuote(Guid quoteId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM quotes WHERE id = $id;";
        command.Parameters.AddWithValue("$id", quoteId.ToString("D"));
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
            """;
        command.ExecuteNonQuery();
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
