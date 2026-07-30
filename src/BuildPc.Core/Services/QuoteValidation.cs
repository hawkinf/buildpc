using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

/// <summary>
/// Regras de orçamento que precisam valer tanto para o SQLite local quanto
/// para o PostgreSQL da API — não só para quem grava pela tela da Montagem.
/// </summary>
/// <remarks>
/// A tela da Montagem já impede digitar um preço abaixo da margem mínima
/// (arredonda para cima), mas nada impedia um item chegar até aqui já abaixo
/// do piso — por um cliente de API direto, por exemplo. Isso deixava a regra
/// "não permita margem menor que 15%" (CONTEXTO_DO_PROJETO.md) reforçada só
/// na interface.
/// </remarks>
public static class QuoteValidation
{
    // Tolerância de 2 centavos para absorver arredondamento decimal legítimo
    // sem rejeitar preços que a própria interface já calculou corretamente.
    private const decimal RoundingTolerance = 0.02m;

    public static void EnsureMinimumMargin(IEnumerable<SavedQuoteItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            if (item.UnitCost <= 0m)
            {
                continue;
            }

            var minimumPrice = item.UnitCost *
                (1 + BusinessSettings.MinimumMarginPercent / 100m);
            if (item.UnitPrice < minimumPrice - RoundingTolerance)
            {
                throw new ArgumentException(
                    $"O preço de venda de \"{item.Name}\" fica abaixo da " +
                    $"margem mínima de {BusinessSettings.MinimumMarginPercent}%.",
                    nameof(items));
            }
        }
    }
}
