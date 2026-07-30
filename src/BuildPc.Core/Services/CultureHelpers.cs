using System.Globalization;

namespace BuildPc.Core.Services;

/// <summary>
/// Formatação de moeda/número em pt-BR, usada tanto pelo Desktop quanto por
/// qualquer cliente novo (ex.: o cliente web) que precise do mesmo
/// arredondamento/formato de preço.
/// </summary>
public static class CultureHelpers
{
    public static CultureInfo BrazilianCulture { get; } = CultureInfo.GetCultureInfo("pt-BR");
}
