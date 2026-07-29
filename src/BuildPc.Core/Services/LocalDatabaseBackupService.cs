using System.Globalization;
using Microsoft.Data.Sqlite;

namespace BuildPc.Core.Services;

/// <summary>
/// Cópias de segurança do banco SQLite local.
/// </summary>
/// <remarks>
/// O modo servidor tem backup diário na VPS, mas o modo local não tinha
/// nenhum: um disco com problema levava catálogo e orçamentos junto. A cópia
/// usa <c>VACUUM INTO</c>, que produz um arquivo consistente mesmo com o WAL
/// ativo — copiar o <c>.db</c> com <c>File.Copy</c> pode gravar um arquivo
/// truncado se houver escrita em andamento.
/// </remarks>
public sealed class LocalDatabaseBackupService(
    string databasePath,
    string backupDirectory,
    int keepCount = 7)
{
    private const string FilePrefix = "catalogo-";
    private const string FileExtension = ".db";

    public string BackupDirectory => backupDirectory;

    /// <summary>
    /// Cria uma cópia se ainda não houver uma de hoje, e descarta as mais
    /// antigas além do limite. Devolve o caminho gravado, ou <c>null</c> quando
    /// a cópia do dia já existia.
    /// </summary>
    public string? CreateDailyBackup(DateTimeOffset now)
    {
        if (!File.Exists(databasePath))
        {
            return null;
        }

        Directory.CreateDirectory(backupDirectory);
        var stamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var destination = Path.Combine(
            backupDirectory,
            $"{FilePrefix}{stamp}{FileExtension}");

        // Uma cópia por dia é suficiente e evita gravar em cada abertura.
        if (File.Exists(destination))
        {
            return null;
        }

        var temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var connection = new SqliteConnection(
                       new SqliteConnectionStringBuilder
                       {
                           DataSource = databasePath,
                           Mode = SqliteOpenMode.ReadOnly,
                           Pooling = false
                       }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();

                // VACUUM INTO não aceita parâmetro; o caminho é interno e as
                // aspas simples são escapadas para não quebrar a instrução.
                command.CommandText =
                    $"VACUUM INTO '{temporary.Replace("'", "''", StringComparison.Ordinal)}';";
                command.ExecuteNonQuery();
            }

            File.Move(temporary, destination, overwrite: true);
            RemoveOldBackups();
            return destination;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public IReadOnlyList<string> ListBackups()
    {
        if (!Directory.Exists(backupDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(backupDirectory, $"{FilePrefix}*{FileExtension}")
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RemoveOldBackups()
    {
        foreach (var path in ListBackups().Skip(keepCount))
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Um arquivo em uso fica para a próxima execução.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
