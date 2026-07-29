using BuildPc.Core.Services;
using Microsoft.Data.Sqlite;

namespace BuildPc.Api.Database;

public static class SqliteSnapshotReader
{
    public static void CreateConsistentBackup(
        string sourceDatabasePath,
        string destinationDatabasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDatabasePath);
        if (!File.Exists(sourceDatabasePath))
        {
            throw new FileNotFoundException(
                "O banco SQLite de origem não foi encontrado.",
                sourceDatabasePath);
        }

        var destinationDirectory = Path.GetDirectoryName(destinationDatabasePath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        using var source = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = sourceDatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
        using var destination = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = destinationDatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ToString());
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    public static async Task<BuildPcDatabaseSnapshot> ReadAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException(
                "O banco SQLite informado não foi encontrado.",
                databasePath);
        }

        var catalog = new ComponentCatalogRepository(databasePath);
        var quotes = new QuoteRepository(databasePath);
        return new BuildPcDatabaseSnapshot(
            await catalog.GetAllAsync(cancellationToken).ConfigureAwait(false),
            await quotes.GetSettingsAsync(cancellationToken).ConfigureAwait(false),
            await quotes.GetQuotesAsync(cancellationToken).ConfigureAwait(false),
            ReadMetadata(databasePath),
            await quotes.GetTemplatesAsync(cancellationToken).ConfigureAwait(false));
    }

    private static IReadOnlyDictionary<string, string> ReadMetadata(string databasePath)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, value FROM app_metadata;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            metadata[reader.GetString(0)] = reader.GetString(1);
        }

        return metadata;
    }
}
