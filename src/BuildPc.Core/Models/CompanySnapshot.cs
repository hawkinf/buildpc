namespace BuildPc.Core.Models;

/// <summary>
/// Dados da empresa gravados junto de um orçamento, para uma configuração
/// futura não alterar um orçamento já emitido.
/// </summary>
/// <remarks>
/// Antes o snapshot era o <see cref="BusinessSettings"/> inteiro, incluindo
/// margem global, margem por categoria e tema — nada disso é usado no PDF ou
/// em qualquer outra leitura de um orçamento gravado, só os dados de
/// identificação da empresa. Guardar o objeto inteiro duplicava a
/// configuração comercial da loja (inclusive as margens) em cada linha da
/// tabela de orçamentos, sem necessidade.
/// </remarks>
public sealed record CompanySnapshot
{
    public string CompanyName { get; init; } = string.Empty;
    public string CompanyDocument { get; init; } = string.Empty;
    public string CompanyPhone { get; init; } = string.Empty;
    public string CompanyEmail { get; init; } = string.Empty;
    public string CompanyWebsite { get; init; } = string.Empty;
    public string CompanyAddress { get; init; } = string.Empty;
    public string LogoPath { get; init; } = string.Empty;
    public string AdditionalQuoteInfo { get; init; } = string.Empty;

    public static CompanySnapshot From(BusinessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new CompanySnapshot
        {
            CompanyName = settings.CompanyName,
            CompanyDocument = settings.CompanyDocument,
            CompanyPhone = settings.CompanyPhone,
            CompanyEmail = settings.CompanyEmail,
            CompanyWebsite = settings.CompanyWebsite,
            CompanyAddress = settings.CompanyAddress,
            LogoPath = settings.LogoPath,
            AdditionalQuoteInfo = settings.AdditionalQuoteInfo
        };
    }
}
