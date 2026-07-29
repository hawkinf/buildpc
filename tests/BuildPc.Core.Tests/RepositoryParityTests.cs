using System.Reflection;
using BuildPc.Api.Database;
using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

/// <summary>
/// Garante que a implementação PostgreSQL não fique atrás da SQLite.
/// </summary>
/// <remarks>
/// Três defeitos já surgiram exatamente desta divergência: o histórico de
/// preços, a contagem de itens que saíram da loja e as colunas de desconto e
/// validade existiam só no SQLite. Estes testes são de estrutura porque não há
/// PostgreSQL disponível na suíte.
/// </remarks>
public sealed class RepositoryParityTests
{
    [Fact]
    public void BothRepositoriesImplementTheSameCatalogInterface()
    {
        Assert.True(typeof(IComponentCatalogRepository)
            .IsAssignableFrom(typeof(ComponentCatalogRepository)));
        Assert.True(typeof(IComponentCatalogRepository)
            .IsAssignableFrom(typeof(PostgresBuildPcRepository)));
        Assert.True(typeof(IComponentCatalogRepository)
            .IsAssignableFrom(typeof(BuildPcApiClient)));
    }

    [Fact]
    public void BothRepositoriesImplementQuotesAndTemplates()
    {
        foreach (var type in new[]
                 {
                     typeof(QuoteRepository),
                     typeof(PostgresBuildPcRepository),
                     typeof(BuildPcApiClient)
                 })
        {
            Assert.True(
                typeof(IQuoteRepository).IsAssignableFrom(type),
                $"{type.Name} deveria implementar IQuoteRepository.");
            Assert.True(
                typeof(IAssemblyTemplateRepository).IsAssignableFrom(type),
                $"{type.Name} deveria implementar IAssemblyTemplateRepository.");
        }
    }

    [Theory]
    // Colunas acrescentadas depois da primeira versão: se uma delas não for
    // gravada ou lida, o dado desaparece em silêncio.
    [InlineData("discount_cents")]
    [InlineData("validity_days")]
    [InlineData("payment_terms")]
    [InlineData("delivery_terms")]
    public void PostgresWritesAndReadsEveryQuoteColumn(string column)
    {
        var source = ReadPostgresSource();

        // Aparece no INSERT, na lista de VALUES, no ON CONFLICT e no SELECT.
        Assert.True(
            CountOccurrences(source, column) >= 4,
            $"A coluna {column} precisa estar no INSERT, VALUES, ON CONFLICT e SELECT " +
            "do PostgreSQL.");
    }

    [Fact]
    public void PostgresHandlesTheFavoriteColumn()
    {
        var source = ReadPostgresSource();

        Assert.Contains("is_favorite", source, StringComparison.Ordinal);
        Assert.Contains("SetFavorite", source, StringComparison.Ordinal);
        // O favorito é reaplicado a partir do estado anterior à substituição.
        Assert.Contains(
            "IsFavorite = previous?.IsFavorite",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PostgresRecordsPriceHistoryAndDisappearedItems()
    {
        var source = ReadPostgresSource();

        Assert.Contains("price_history", source, StringComparison.Ordinal);
        Assert.Contains("RecordPriceChange", source, StringComparison.Ordinal);
        Assert.Contains("disappeared", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationSnapshotCarriesTemplates()
    {
        // Modelos são dados do usuário: sem eles na migração, desapareceriam.
        var snapshotType = typeof(BuildPcDatabaseSnapshot);
        var templates = snapshotType.GetProperty(
            "Templates",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(templates);
        Assert.Equal(
            typeof(IReadOnlyList<AssemblyTemplate>),
            templates.PropertyType);

        var source = ReadPostgresSource();
        Assert.Contains("snapshot.Templates", source, StringComparison.Ordinal);
        Assert.Contains("InsertTemplate", source, StringComparison.Ordinal);
        // A migração limpa os modelos antigos junto com o resto.
        Assert.Contains(
            "DELETE FROM assembly_templates;",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotWithoutTemplatesStillConstructs()
    {
        // Sobrecarga de compatibilidade, para snapshots anteriores.
        var snapshot = new BuildPcDatabaseSnapshot(
            [],
            new BusinessSettings(),
            [],
            new Dictionary<string, string>());

        Assert.Empty(snapshot.Templates);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string ReadPostgresSource() =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BuildPc.Api",
            "Database",
            "PostgresBuildPcRepository.cs"));

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[]
                 {
                     Directory.GetCurrentDirectory(),
                     AppContext.BaseDirectory
                 })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "BuildPc.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Não foi possível localizar a raiz do repositório BuildPC.");
    }
}
