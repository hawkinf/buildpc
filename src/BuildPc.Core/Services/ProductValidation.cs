using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

/// <summary>
/// Validação de produto compartilhada entre o SQLite local e o PostgreSQL da
/// API — sem isto, um cliente autenticado da API podia gravar preço/potência
/// negativos ou uma categoria fora do enum (que depois vira produto
/// invisível em toda lista filtrada, sem nenhum erro visível ao usuário).
/// </summary>
public static class ProductValidation
{
    public static void EnsureValid(PcComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (component.Price < 0m)
        {
            throw new ArgumentException(
                "O preço do produto não pode ser negativo.",
                nameof(component));
        }

        if (component.PowerWatts < 0)
        {
            throw new ArgumentException(
                "A potência do produto não pode ser negativa.",
                nameof(component));
        }

        if (!Enum.IsDefined(component.Category))
        {
            throw new ArgumentException(
                "A categoria do produto é inválida.",
                nameof(component));
        }
    }
}
