using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

/// <summary>
/// Formato das chaves de metadados de importação, compartilhado pelos
/// repositórios SQLite e PostgreSQL e pelo cliente da API.
/// </summary>
public static class ImportKeys
{
    public const string MetadataPrefix = "last_import:";

    /// <summary>Chave de uma categoria em uma origem, como <c>kabum:3</c>.</summary>
    public static string For(ComponentCategory category, string source) =>
        $"{source.Trim().ToLowerInvariant()}:{(int)category}";

    public static string MetadataKey(ComponentCategory category, string source) =>
        $"{MetadataPrefix}{For(category, source)}";
}
