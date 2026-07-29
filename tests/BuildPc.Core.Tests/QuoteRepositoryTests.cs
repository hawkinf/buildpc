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
                new QuoteDraft
                {
                    ClientName = "Maria",
                    ClientPhone = "(11) 98888-0000",
                    Notes = "Entrega combinada",
                    Items =
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
                    CompanySnapshot = loadedSettings
                });

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
                new QuoteDraft
                {
                    ClientName = "Maria Atualizada",
                    ClientPhone = loaded.ClientPhone,
                    Notes = loaded.Notes,
                    Items = loaded.Items,
                    CompanySnapshot = loaded.CompanySnapshot
                });
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

    [Fact]
    public void QuoteKeepsDiscountValidityAndCommercialTerms()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-quote-terms-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var repository = new QuoteRepository(Path.Combine(directory, "catalogo.db"));

            var saved = repository.SaveQuote(
                null,
                new QuoteDraft
                {
                    ClientName = "Nina",
                    ClientPhone = "(11) 91111-0000",
                    Items = [Item(quantity: 2, unitPrice: 500m)],
                    CompanySnapshot = new BusinessSettings(),
                    DiscountAmount = 150m,
                    ValidityDays = 15,
                    PaymentTerms = "3x sem juros",
                    DeliveryTerms = "Retirada em loja"
                });

            Assert.Equal(1000m, saved.TotalPrice);
            Assert.Equal(150m, saved.DiscountAmount);
            Assert.Equal(850m, saved.FinalPrice);
            Assert.True(saved.HasDiscount);
            Assert.Equal(saved.CreatedAt.AddDays(15), saved.ValidUntil);

            var loaded = Assert.Single(repository.GetQuotes());
            Assert.Equal(150m, loaded.DiscountAmount);
            Assert.Equal(850m, loaded.FinalPrice);
            Assert.Equal(15, loaded.ValidityDays);
            Assert.Equal("3x sem juros", loaded.PaymentTerms);
            Assert.Equal("Retirada em loja", loaded.DeliveryTerms);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void DiscountNeverExceedsTheTotalNorGoesNegative()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-quote-desconto-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var repository = new QuoteRepository(Path.Combine(directory, "catalogo.db"));

            // Um desconto acima do total zeraria a venda e inverteria o lucro.
            var saved = repository.SaveQuote(
                null,
                new QuoteDraft
                {
                    ClientName = "Otto",
                    ClientPhone = "(11) 92222-0000",
                    Items = [Item(quantity: 1, unitPrice: 100m)],
                    CompanySnapshot = new BusinessSettings(),
                    DiscountAmount = 500m
                });

            Assert.Equal(100m, saved.DiscountAmount);
            Assert.Equal(0m, saved.FinalPrice);

            Assert.Throws<ArgumentException>(() => repository.SaveQuote(
                null,
                new QuoteDraft
                {
                    ClientName = "Otto",
                    ClientPhone = "(11) 92222-0000",
                    Items = [Item(quantity: 1, unitPrice: 100m)],
                    CompanySnapshot = new BusinessSettings(),
                    DiscountAmount = -10m
                }));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void QuotesSavedBeforeTheNewFieldsRemainReadable()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-quote-legado-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "catalogo.db");
            var repository = new QuoteRepository(path);
            var saved = SaveQuote(repository, "Legado");

            // Simula o registro de uma versão anterior, sem os campos novos.
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(
                       new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                       {
                           DataSource = path,
                           Pooling = false
                       }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    UPDATE quotes
                    SET discount_cents = 0, validity_days = 0,
                        payment_terms = '', delivery_terms = ''
                    WHERE id = $id;
                    """;
                command.Parameters.AddWithValue("$id", saved.Id.ToString("D"));
                command.ExecuteNonQuery();
            }

            var loaded = Assert.Single(repository.GetQuotes());
            Assert.Equal(0m, loaded.DiscountAmount);
            Assert.False(loaded.HasDiscount);
            Assert.Null(loaded.ValidUntil);
            Assert.Equal(loaded.TotalPrice, loaded.FinalPrice);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static SavedQuoteItem Item(int quantity, decimal unitPrice) =>
        new()
        {
            ComponentId = "cpu",
            Category = ComponentCategory.Processor,
            CategoryName = "Processador",
            Name = "CPU Teste",
            Description = "Descrição",
            Quantity = quantity,
            UnitCost = unitPrice / 2m,
            MarginPercent = 100m,
            UnitPrice = unitPrice
        };

    private static SavedQuote SaveQuote(QuoteRepository repository, string clientName) =>
        repository.SaveQuote(
            null,
            new QuoteDraft
            {
                ClientName = clientName,
                ClientPhone = "(11) 90000-0000",
                Items =
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
                CompanySnapshot = new BusinessSettings()
            });
}
