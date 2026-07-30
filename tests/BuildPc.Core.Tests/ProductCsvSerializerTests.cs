using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

public sealed class ProductCsvSerializerTests
{
    [Fact]
    public void RoundTripPreservesEveryField()
    {
        var original = new PcComponent
        {
            Id = "custom-1",
            Category = ComponentCategory.Motherboard,
            Name = "Placa-mãe X",
            Brand = "ASUS",
            Description = "ATX • AM5 • DDR5",
            Price = 1649.90m,
            PowerWatts = 40,
            Socket = "AM5",
            MemoryType = "DDR5",
            FormFactor = "ATX",
            ImageUrl = "https://exemplo/img.jpg",
            ImportSource = "kabum",
            KeepOnImport = true,
            IsFavorite = true
        };

        var result = ProductCsvSerializer.Parse(
            ProductCsvSerializer.BuildCsv([original]));

        Assert.False(result.HasErrors);
        var parsed = Assert.Single(result.Products);
        Assert.Equal(original.Id, parsed.Id);
        Assert.Equal(original.Category, parsed.Category);
        Assert.Equal(original.Name, parsed.Name);
        Assert.Equal(original.Brand, parsed.Brand);
        Assert.Equal(original.Description, parsed.Description);
        Assert.Equal(original.Price, parsed.Price);
        Assert.Equal(original.PowerWatts, parsed.PowerWatts);
        Assert.Equal(original.Socket, parsed.Socket);
        Assert.Equal(original.MemoryType, parsed.MemoryType);
        Assert.Equal(original.FormFactor, parsed.FormFactor);
        Assert.Equal(original.ImageUrl, parsed.ImageUrl);
        Assert.Equal(original.ImportSource, parsed.ImportSource);
        Assert.True(parsed.KeepOnImport);
        Assert.True(parsed.IsFavorite);
    }

    [Fact]
    public void FieldsWithSeparatorsQuotesAndAccentsSurviveTheRoundTrip()
    {
        var original = new PcComponent
        {
            Id = "custom-2",
            Category = ComponentCategory.Memory,
            Name = "Memória 16GB; kit \"gamer\"",
            Brand = "Marca; com ponto e vírgula",
            Description = "Descrição com \"aspas\" e ; separador",
            Price = 459.90m
        };

        var result = ProductCsvSerializer.Parse(
            ProductCsvSerializer.BuildCsv([original]));

        var parsed = Assert.Single(result.Products);
        Assert.Equal(original.Name, parsed.Name);
        Assert.Equal(original.Brand, parsed.Brand);
        Assert.Equal(original.Description, parsed.Description);
    }

    [Theory]
    // A planilha do usuário pode vir com qualquer das duas configurações.
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1234,56", 1234.56)]
    [InlineData("1234.56", 1234.56)]
    [InlineData("R$ 99,90", 99.90)]
    // Tres digitos depois do separador significam agrupamento, nao centavos.
    [InlineData("1.234", 1234)]
    [InlineData("1234", 1234)]
    [InlineData("1.234.567,89", 1234567.89)]
    public void PriceAcceptsBothDecimalSeparators(string text, double expected)
    {
        var csv = $"custom-3;3;Memória;Marca;Descrição;{text}";

        var result = ProductCsvSerializer.Parse(csv);

        Assert.False(result.HasErrors);
        Assert.Equal((decimal)expected, Assert.Single(result.Products).Price);
    }

    [Fact]
    public void HeaderRowIsIgnored()
    {
        var csv = string.Join(
            "\n",
            "id;categoria;nome;marca;descricao;custo",
            "custom-4;0;CPU;AMD;Descrição;100,00");

        var result = ProductCsvSerializer.Parse(csv);

        Assert.Single(result.Products);
        Assert.Equal("custom-4", result.Products[0].Id);
    }

    [Fact]
    public void BadLinesAreReportedWithoutBlockingTheGoodOnes()
    {
        var csv = string.Join(
            "\n",
            "custom-ok;0;CPU boa;AMD;Descrição;100,00",
            "faltam-colunas;0",
            ";0;Sem id;AMD;Descrição;100,00",
            "custom-sem-nome;0;;AMD;Descrição;100,00",
            "custom-categoria;abc;CPU;AMD;Descrição;100,00",
            "custom-preco;0;CPU;AMD;Descrição;nao-e-numero",
            "custom-ok;0;Id repetido;AMD;Descrição;100,00",
            "custom-ok2;0;CPU boa 2;AMD;Descrição;200,00");

        var result = ProductCsvSerializer.Parse(csv);

        Assert.Equal(2, result.Products.Count);
        Assert.Equal(["custom-ok", "custom-ok2"], result.Products.Select(p => p.Id));
        Assert.True(result.HasErrors);
        Assert.Equal(6, result.Errors.Count);
        Assert.Contains(result.Errors, error => error.Contains("6 colunas"));
        Assert.Contains(result.Errors, error => error.Contains("identificador está vazio"));
        Assert.Contains(result.Errors, error => error.Contains("nome está vazio"));
        Assert.Contains(result.Errors, error => error.Contains("categoria inválida"));
        Assert.Contains(result.Errors, error => error.Contains("repetido"));
    }

    [Fact]
    public void NegativePriceIsRejected()
    {
        var result = ProductCsvSerializer.Parse(
            "custom-5;0;CPU;AMD;Descrição;-10,00");

        Assert.Empty(result.Products);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ZeroPriceIsRejected()
    {
        // Um custo zerado zeraria também o preço de venda (custo x margem) e
        // quebraria qualquer cálculo de lucro percentual (divisão por zero).
        var result = ProductCsvSerializer.Parse(
            "custom-zero;0;CPU;AMD;Descrição;0,00");

        Assert.Empty(result.Products);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void CategoryOutsideTheEnumIsRejected()
    {
        // Antes virava um produto "fantasma": gravado, mas invisível em toda
        // lista filtrada por categoria, sem nenhum erro visível ao usuário.
        var result = ProductCsvSerializer.Parse(
            "custom-categoria;999;CPU;AMD;Descrição;100,00");

        Assert.Empty(result.Products);
        Assert.Contains(result.Errors, error => error.Contains("categoria inválida"));
    }

    [Fact]
    public void EmptyCsvProducesNothingAndNoError()
    {
        var result = ProductCsvSerializer.Parse(string.Empty);

        Assert.Empty(result.Products);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ProductWithoutImportSourceIsTreatedAsManual()
    {
        var result = ProductCsvSerializer.Parse(
            "custom-6;0;CPU;AMD;Descrição;100,00;0;;;;;;0;0");

        var parsed = Assert.Single(result.Products);
        Assert.True(parsed.IsUserDefined);
        Assert.Null(parsed.ImportSource);
    }
}
