using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

/// <summary>
/// Cálculo de preço de venda a partir do custo e da margem, compartilhado
/// entre o Desktop e qualquer cliente novo (ex.: o cliente web) — a regra de
/// arredondamento terminando em ",90" é a mesma em todo lugar que mostra
/// preço ao cliente, então só existe uma implementação dela.
/// </summary>
public static class PricingCalculator
{
    public static decimal CalculateSalePrice(decimal cost, decimal margin) =>
        RoundUpToNinetyCents(
            cost * (1m + Math.Max(BusinessSettings.MinimumMarginPercent, margin) / 100m));

    public static decimal RoundUpToNinetyCents(decimal value)
    {
        if (value <= 0)
        {
            return 0.90m;
        }

        var wholePart = decimal.Floor(value);
        var priceEndingInNinety = wholePart + 0.90m;
        return value <= priceEndingInNinety
            ? priceEndingInNinety
            : wholePart + 1.90m;
    }
}
