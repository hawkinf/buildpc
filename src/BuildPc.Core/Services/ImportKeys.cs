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

    /// <summary>
    /// Chave da URL de importação configurada para uma categoria, como
    /// <c>"kabum:Processor"</c> ou <c>"kabum-hd:HardDrive"</c>. Formato
    /// diferente de <see cref="For"/> de propósito (nome do enum, não
    /// minúsculo nem convertido para int) — usado tanto pelo arquivo local
    /// do Desktop quanto pela entrada compartilhada no servidor
    /// (<c>IQuoteRepository.GetImportSourceUrlsAsync</c>), então precisa
    /// bater exatamente nos dois clientes.
    /// </summary>
    public static string SourceUrlKey(ComponentCategory category, string sourceKey) =>
        $"{sourceKey}:{category}";
}
