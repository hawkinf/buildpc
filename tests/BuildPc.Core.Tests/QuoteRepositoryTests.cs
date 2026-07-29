using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

public sealed class QuoteRepositoryTests
{
    [Fact]
    public void SettingsAndQuote_RoundTripThroughSqlite()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-quote-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var repository = new QuoteRepository(Path.Combine(directory, "catalogo.db"));
            var settings = new BusinessSettings
            {
                GlobalMarginPercent = 25m,
                CategoryMargins = new Dictionary<ComponentCategory, decimal>
                {
                    [ComponentCategory.GraphicsCard] = 18.5m
                },
                CompanyName = "Empresa Teste",
                CompanyPhone = "(11) 99999-9999",
                AdditionalQuoteInfo = "Proposta válida por 7 dias.",
                ThemeMode = AppThemeMode.Light,
                ProductCategories =
                [
                    .. ProductCategoryDefinition.Defaults(),
                    new ProductCategoryDefinition
                    {
                        Value = (ComponentCategory)1000,
                        Name = "Acessórios",
                        DisplayOrder = 12,
                        IsSystem = false
                    }
                ]
            };
            repository.SaveSettings(settings);

            var loadedSettings = repository.GetSettings();
            Assert.Equal(25m, loadedSettings.GlobalMarginPercent);
            Assert.Equal(18.5m, loadedSettings.MarginFor(ComponentCategory.GraphicsCard));
            Assert.Equal(25m, loadedSettings.MarginFor(ComponentCategory.Memory));
            Assert.Equal(AppThemeMode.Light, loadedSettings.ThemeMode);
            Assert.Contains(
                loadedSettings.EffectiveProductCategories(),
                category =>
                    category.Value == (ComponentCategory)1000 &&
                    category.Name == "Acessórios" &&
                    !category.IsSystem);

            var saved = repository.SaveQuote(
                null,
                "Maria",
                "(11) 98888-0000",
                "Entrega combinada",
                [
                    new SavedQuoteItem
                    {
                        ComponentId = "cpu",
                        Category = ComponentCategory.Processor,
                        CategoryName = "Processador",
                        Name = "CPU Teste",
                        Description = "Descrição",
                        Quantity = 2,
                        UnitCost = 100m,
                        MarginPercent = 25m,
                        UnitPrice = 125m
                    }
                ],
                loadedSettings);

            var loaded = Assert.Single(repository.GetQuotes());
            Assert.Equal(saved.Id, loaded.Id);
            Assert.Equal(1, loaded.Number);
            Assert.Equal(200m, loaded.TotalCost);
            Assert.Equal(250m, loaded.TotalPrice);
            Assert.Equal(50m, loaded.TotalProfit);
            Assert.Equal(100m, Assert.Single(loaded.Items).UnitCost);
            Assert.Equal(125m, Assert.Single(loaded.Items).UnitPrice);

            var updated = repository.SaveQuote(
                loaded,
                "Maria Atualizada",
                loaded.ClientPhone,
                loaded.Notes,
                loaded.Items,
                loaded.CompanySnapshot);
            Assert.Equal(loaded.Id, updated.Id);
            Assert.Equal(loaded.Number, updated.Number);
            Assert.Equal("Maria Atualizada", Assert.Single(repository.GetQuotes()).ClientName);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void DeleteQuoteRemovesOnlyTheRequestedQuoteAndKeepsNumbering()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-quote-delete-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var repository = new QuoteRepository(Path.Combine(directory, "catalogo.db"));
            var first = SaveQuote(repository, "Ana");
            var second = SaveQuote(repository, "Bruno");
            Assert.Equal(1, first.Number);
            Assert.Equal(2, second.Number);

            Assert.True(repository.DeleteQuote(first.Id));

            var remaining = Assert.Single(repository.GetQuotes());
            Assert.Equal(second.Id, remaining.Id);
            Assert.Equal(2, remaining.Number);

            // Um orçamento já excluído não é erro, apenas não altera nada.
            Assert.False(repository.DeleteQuote(first.Id));
            Assert.False(repository.DeleteQuote(Guid.NewGuid()));

            // O número apagado nunca volta a ser usado.
            Assert.Equal(3, SaveQuote(repository, "Carla").Number);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static SavedQuote SaveQuote(QuoteRepository repository, string clientName) =>
        repository.SaveQuote(
            null,
            clientName,
            "(11) 90000-0000",
            string.Empty,
            [
                new SavedQuoteItem
                {
                    ComponentId = "cpu",
                    Category = ComponentCategory.Processor,
                    CategoryName = "Processador",
                    Name = "CPU Teste",
                    Description = "Descrição",
                    Quantity = 1,
                    UnitCost = 100m,
                    MarginPercent = 25m,
                    UnitPrice = 125m
                }
            ],
            new BusinessSettings());
}
