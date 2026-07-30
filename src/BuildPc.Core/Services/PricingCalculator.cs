using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

/// <summary>
/// Cálculo de preço de venda a partir do custo e da margem, compartilhado
/// entre o Desktop e qualquer cliente novo (ex.: o cliente web, inclusive a
/// importação) — a regra de arredondamento é a mesma em todo lugar que
/// calcula preço de venda a partir do custo, então só existe uma
/// implementação dela.
/// </summary>
public static class PricingCalculator
{
    public static decimal CalculateSalePrice(decimal cost, decimal margin) =>
        RoundUpToNinetyCents(
            cost * (1m + Math.Max(BusinessSettings.MinimumMarginPercent, margin) / 100m));

    /// <summary>
    /// Arredonda para cima até o próximo valor terminado em ",90", a cada
    /// 5 reais (4,90; 9,90; 14,90; 19,90; 24,90...) — nunca a cada 1 real
    /// como antes. Ex.: 81,20 → 84,90 (não 81,90); 84,91 → 89,90.
    /// </summary>
    public static decimal RoundUpToNinetyCents(decimal value)
    {
        if (value <= 0)
        {
            return 4.90m;
        }

        var step = Math.Ceiling((value + 0.10m) / 5m);
        return step * 5m - 0.10m;
    }
}
