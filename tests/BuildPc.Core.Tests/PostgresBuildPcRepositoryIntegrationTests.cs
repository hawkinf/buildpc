using BuildPc.Api.Database;
using BuildPc.Core.Models;
using Npgsql;
using Testcontainers.PostgreSql;

namespace BuildPc.Core.Tests;

/// <summary>
/// Cobre <see cref="PostgresBuildPcRepository"/> contra um PostgreSQL de
/// verdade, dentro de um contêiner descartável.
/// </summary>
/// <remarks>
/// <see cref="RepositoryParityTests"/> só confirma que uma coluna aparece no
/// texto-fonte — não pega, por exemplo, uma futura reordenação do SELECT sem
/// atualizar os índices posicionais de leitura. Isto aqui grava e lê de
/// volta com valores não triviais, então uma regressão desse tipo derruba um
/// teste de verdade.
///
/// Precisa de Docker. Sem ele, <see cref="PostgresFixture.InitializeAsync"/>
/// falha ao subir o contêiner; cada teste então só registra a saída e
/// retorna, em vez de falhar a suíte inteira — máquinas de desenvolvimento
/// sem Docker continuam com `dotnet test` limpo.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public bool IsAvailable { get; private set; }

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("buildpc_test")
                .WithUsername("buildpc_test")
                .WithPassword("buildpc_test")
                .Build();
            await _container.StartAsync();
            DataSource = NpgsqlDataSource.Create(_container.GetConnectionString());
            IsAvailable = true;
        }
        catch
        {
            // Docker indisponível ou sem permissão para subir contêineres:
            // os testes desta classe se tornam no-ops (ver IsAvailable).
            IsAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

[Collection("Postgres")]
public sealed class PostgresBuildPcRepositoryIntegrationTests(PostgresFixture fixture)
{
    [Fact]
    public void ProductRoundTripsWithEveryField()
    {
        if (!Skip()) return;

        var repository = new PostgresBuildPcRepository(fixture.DataSource);
        var component = new PcComponent
        {
            Id = "int-cpu-1",
            Category = ComponentCategory.Processor,
            Name = "Ryzen Teste",
            Brand = "AMD",
            Description = "Descrição de teste",
            Price = 1234.56m,
            PowerWatts = 105,
            Socket = "AM5",
            MemoryType = "DDR5",
            FormFactor = "ATX",
            SupportedSockets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AM5" },
            SupportedFormFactors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ATX" },
            ImageUrl = "https://example.com/cpu.jpg",
            IsUserDefined = true
        };

        repository.Add(component);
        var loaded = repository.GetAll().Single(item => item.Id == "int-cpu-1");

        Assert.Equal(component.Name, loaded.Name);
        Assert.Equal(component.Price, loaded.Price);
        Assert.Equal(component.PowerWatts, loaded.PowerWatts);
        Assert.Equal(component.Socket, loaded.Socket);
        Assert.Equal(component.MemoryType, loaded.MemoryType);
        Assert.Equal(component.FormFactor, loaded.FormFactor);
        Assert.Contains("AM5", loaded.SupportedSockets);
        Assert.Contains("ATX", loaded.SupportedFormFactors);
        Assert.Equal(component.ImageUrl, loaded.ImageUrl);
    }

    [Fact]
    public void UpdateRecordsPriceHistoryWithManualSource()
    {
        if (!Skip()) return;

        var repository = new PostgresBuildPcRepository(fixture.DataSource);
        var component = new PcComponent
        {
            Id = "int-mem-1",
            Category = ComponentCategory.Memory,
            Name = "Memória Teste",
            Brand = "Teste",
            Description = "Descrição",
            Price = 100m
        };
        repository.Add(component);

        Assert.True(repository.Update(component with { Price = 150m }));

        var history = Assert.Single(repository.GetPriceHistory("int-mem-1"));
        Assert.Equal(100m, history.Price);
        Assert.Equal("manual", history.Source);
    }

    [Fact]
    public void ReplaceImportedPreservesFavoriteAndRecordsPriceChange()
    {
        if (!Skip()) return;

        var repository = new PostgresBuildPcRepository(fixture.DataSource);
        var original = new PcComponent
        {
            Id = "int-gpu-1",
            Category = ComponentCategory.GraphicsCard,
            Name = "Placa de vídeo Teste",
            Brand = "Teste",
            Description = "Descrição",
            Price = 2000m,
            ImportSource = "kabum"
        };
        repository.ReplaceImported(ComponentCategory.GraphicsCard, "kabum", [original]);
        Assert.True(repository.SetFavorite("int-gpu-1", true));

        var updated = original with { Price = 2200m };
        var result = repository.ReplaceImported(
            ComponentCategory.GraphicsCard,
            "kabum",
            [updated]);

        var reloaded = repository.GetAll().Single(item => item.Id == "int-gpu-1");
        Assert.True(reloaded.IsFavorite);
        Assert.Equal(2200m, reloaded.Price);
        Assert.Single(result.PriceChanges);
        Assert.Equal(2000m, Assert.Single(repository.GetPriceHistory("int-gpu-1")).Price);
    }

    [Fact]
    public void QuoteRoundTripsWithDiscountValidityAndTerms()
    {
        if (!Skip()) return;

        var repository = new PostgresBuildPcRepository(fixture.DataSource);
        var saved = repository.SaveQuote(
            null,
            new QuoteDraft
            {
                ClientName = "Cliente Teste",
                ClientPhone = "(11) 90000-0000",
                Items =
                [
                    new SavedQuoteItem
                    {
                        ComponentId = "int-cpu-1",
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
                CompanySnapshot = new CompanySnapshot { CompanyName = "Loja Teste" },
                DiscountAmount = 10m,
                ValidityDays = 7,
                PaymentTerms = "3x sem juros",
                DeliveryTerms = "Retirada em loja"
            });

        var loaded = repository.GetQuotes().Single(quote => quote.Id == saved.Id);
        Assert.Equal(10m, loaded.DiscountAmount);
        Assert.Equal(7, loaded.ValidityDays);
        Assert.Equal("3x sem juros", loaded.PaymentTerms);
        Assert.Equal("Retirada em loja", loaded.DeliveryTerms);
        Assert.Equal("Loja Teste", loaded.CompanySnapshot.CompanyName);
    }

    /// <returns>
    /// <c>false</c> quando o Docker está indisponível: o chamador deve
    /// retornar imediatamente sem asserções, em vez de falhar.
    /// </returns>
    private bool Skip()
    {
        if (fixture.IsAvailable)
        {
            return true;
        }

        Console.WriteLine(
            "PostgresBuildPcRepositoryIntegrationTests: Docker indisponível, teste não executado.");
        return false;
    }
}
