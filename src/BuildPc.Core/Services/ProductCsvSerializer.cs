using System.Globalization;
using System.Text;
using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

/// <summary>Resultado da leitura de um CSV de produtos.</summary>
public sealed record ProductCsvImport
{
    public IReadOnlyList<PcComponent> Products { get; init; } = [];

    /// <summary>
    /// Linhas recusadas, com o motivo. Uma linha ruim não impede o restante da
    /// importação, mas o usuário precisa saber o que ficou de fora.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool HasErrors => Errors.Count > 0;
}

/// <summary>
/// Exporta e importa o catálogo em CSV, para o usuário conseguir tirar os dados
/// do programa e editá-los em planilha.
/// </summary>
/// <remarks>
/// O separador é ponto e vírgula e os números usam vírgula decimal, que é o que
/// o Excel em português espera. O arquivo é gravado com BOM UTF-8 pelo mesmo
/// motivo: sem ele o Excel corrompe acentos.
/// </remarks>
public static class ProductCsvSerializer
{
    private const char Separator = ';';

    private static readonly string[] Columns =
    [
        "id",
        "categoria",
        "nome",
        "marca",
        "descricao",
        "custo",
        "watts",
        "socket",
        "memoria",
        "formato",
        "imagem",
        "origem",
        "manter",
        "favorito"
    ];

    public static string BuildCsv(IEnumerable<PcComponent> products)
    {
        ArgumentNullException.ThrowIfNull(products);
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(Separator, Columns));
        foreach (var product in products)
        {
            builder.AppendLine(string.Join(Separator,
                Escape(product.Id),
                ((int)product.Category).ToString(CultureInfo.InvariantCulture),
                Escape(product.Name),
                Escape(product.Brand),
                Escape(product.Description),
                product.Price.ToString("0.00", MainCulture),
                product.PowerWatts.ToString(CultureInfo.InvariantCulture),
                Escape(product.Socket),
                Escape(product.MemoryType),
                Escape(product.FormFactor),
                Escape(product.ImageUrl),
                Escape(product.ImportSource),
                product.KeepOnImport ? "1" : "0",
                product.IsFavorite ? "1" : "0"));
        }

        return builder.ToString();
    }

    public static ProductCsvImport Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var products = new List<PcComponent>();
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = csv.ReplaceLineEndings("\n").Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // A primeira linha pode ser o cabeçalho gravado pela exportação.
            if (index == 0 &&
                line.StartsWith("id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fields = SplitLine(line);
            if (fields.Count < 6)
            {
                errors.Add($"Linha {index + 1}: são necessárias ao menos 6 colunas.");
                continue;
            }

            var id = fields[0].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"Linha {index + 1}: o identificador está vazio.");
                continue;
            }

            if (!seen.Add(id))
            {
                errors.Add($"Linha {index + 1}: identificador repetido \"{id}\".");
                continue;
            }

            if (!int.TryParse(
                    fields[1].Trim(),
                    CultureInfo.InvariantCulture,
                    out var categoryValue) ||
                !Enum.IsDefined((ComponentCategory)categoryValue))
            {
                // Um valor fora do enum não dava erro nenhum: o produto
                // entrava no catálogo e sumia de toda lista filtrada por
                // categoria, sem nenhum aviso ao usuário.
                errors.Add($"Linha {index + 1}: categoria inválida.");
                continue;
            }

            var name = fields[2].Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add($"Linha {index + 1}: o nome está vazio.");
                continue;
            }

            if (!TryParsePrice(fields[5], out var price) || price <= 0)
            {
                errors.Add(
                    $"Linha {index + 1}: custo inválido \"{fields[5].Trim()}\".");
                continue;
            }

            products.Add(new PcComponent
            {
                Id = id,
                Category = (ComponentCategory)categoryValue,
                Name = name,
                Brand = Field(fields, 3),
                Description = Field(fields, 4),
                Price = price,
                PowerWatts = int.TryParse(
                    Field(fields, 6),
                    CultureInfo.InvariantCulture,
                    out var watts)
                    ? watts
                    : 0,
                Socket = NullIfEmpty(Field(fields, 7)),
                MemoryType = NullIfEmpty(Field(fields, 8)),
                FormFactor = NullIfEmpty(Field(fields, 9)),
                ImageUrl = NullIfEmpty(Field(fields, 10)),
                ImportSource = NullIfEmpty(Field(fields, 11)),
                KeepOnImport = IsTrue(Field(fields, 12)),
                IsFavorite = IsTrue(Field(fields, 13)),
                IsUserDefined = string.IsNullOrWhiteSpace(Field(fields, 11))
            });
        }

        return new ProductCsvImport
        {
            Products = products,
            Errors = errors
        };
    }

    private static CultureInfo MainCulture { get; } =
        CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Aceita vírgula ou ponto como separador decimal: a planilha do usuário
    /// pode ter sido salva em qualquer das duas configurações regionais.
    /// </summary>
    /// <remarks>
    /// Tentar uma cultura e depois a outra não funciona: em pt-BR o ponto é
    /// separador de milhar, então <c>1234.56</c> viraria 123456 em silêncio. A
    /// decisão vem da posição do último separador — se sobram uma ou duas casas,
    /// ele é decimal; se sobram três, é agrupamento.
    /// </remarks>
    private static bool TryParsePrice(string value, out decimal price)
    {
        price = 0m;
        var text = value
            .Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim();
        if (text.Length == 0)
        {
            return false;
        }

        var lastSeparator = text.LastIndexOfAny(['.', ',']);
        if (lastSeparator < 0)
        {
            return decimal.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out price);
        }

        var decimals = text.Length - lastSeparator - 1;
        var integerPart = text[..lastSeparator]
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);
        var fractionPart = text[(lastSeparator + 1)..];

        // Três dígitos após o último separador indicam agrupamento (1.234), não
        // centavos; nesse caso o valor inteiro é tudo junto.
        var normalized = decimals is 1 or 2
            ? $"{integerPart}.{fractionPart}"
            : integerPart + fractionPart;

        return decimal.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out price);
    }

    private static List<string> SplitLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var insideQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                // Duas aspas seguidas dentro de um campo representam uma aspa.
                if (insideQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                insideQuotes = !insideQuotes;
                continue;
            }

            if (character == Separator && !insideQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        if (!text.Contains(Separator) &&
            !text.Contains('"') &&
            !text.Contains('\n') &&
            !text.Contains('\r'))
        {
            return text;
        }

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static string Field(List<string> fields, int index) =>
        index < fields.Count ? fields[index].Trim() : string.Empty;

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool IsTrue(string value) =>
        value.Trim() is "1" or "sim" or "SIM" or "true" or "True" or "verdadeiro";
}
