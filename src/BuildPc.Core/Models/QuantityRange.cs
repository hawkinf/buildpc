namespace BuildPc.Core.Models;

/// <summary>
/// Limites de quantidade de um item de orçamento.
/// </summary>
/// <remarks>
/// O limite anterior de 100 era artificial e vinha de a interface usar um
/// <c>ComboBox</c> preenchido com cem entradas. Itens como cabos, conectores ou
/// licenças passam disso com facilidade.
/// </remarks>
public static class QuantityRange
{
    public const int Minimum = 1;

    /// <summary>
    /// Teto alto o suficiente para qualquer venda real, mantido apenas para
    /// evitar que um erro de digitação gere um total absurdo.
    /// </summary>
    public const int Maximum = 9999;

    public static int Clamp(int quantity) =>
        Math.Clamp(quantity, Minimum, Maximum);
}
