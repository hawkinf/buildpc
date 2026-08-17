using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

/// <summary>
/// URLs de origem padrão por categoria e a chave de origem correspondente.
///
/// Estava embutido na página de Importações, o que bastava enquanto só a tela
/// importava. Com a importação diária automática (ImportacaoDiariaHostedService)
/// passaram a existir dois chamadores, e duplicar a lista significaria que uma
/// mudança numa URL valeria só para quem clica no botão — a rotina da manhã
/// continuaria buscando do endereço antigo, sem ninguém perceber.
/// </summary>
public static class ImportSourceDefaults
{
    private const string Query =
        "?page_number=1&page_size=60&facet_filters=eyJrYWJ1bV9wcm9kdWN0IjpbInRydWUiXX0=&sort=most_searched";

    /// <summary>
    /// Mesmas URLs configuradas no Desktop (buildpc.config.json /
    /// MainWindowViewModel.ImportSource). São ponto de partida: o que estiver
    /// gravado no servidor tem precedência.
    /// </summary>
    public static IReadOnlyDictionary<ComponentCategory, string> Urls { get; } =
        new Dictionary<ComponentCategory, string>
        {
            [ComponentCategory.HardDrive] = $"https://www.kabum.com.br/hardware/disco-rigido-hd{Query}",
            [ComponentCategory.Processor] = $"https://www.kabum.com.br/hardware/processadores{Query}",
            [ComponentCategory.Cooler] = $"https://www.kabum.com.br/hardware/coolers{Query}",
            [ComponentCategory.Motherboard] = $"https://www.kabum.com.br/hardware/placas-mae{Query}",
            [ComponentCategory.Memory] = $"https://www.kabum.com.br/hardware/memoria-ram{Query}",
            [ComponentCategory.GraphicsCard] = $"https://www.kabum.com.br/hardware/placa-de-video-vga{Query}",
            [ComponentCategory.Storage] = $"https://www.kabum.com.br/hardware/ssd-2-5{Query}",
            [ComponentCategory.PowerSupply] = $"https://www.kabum.com.br/hardware/fontes{Query}",
            [ComponentCategory.Case] = $"https://www.kabum.com.br/perifericos/gabinetes{Query}",
            [ComponentCategory.Monitor] = $"https://www.kabum.com.br/computadores/monitores{Query}",
            [ComponentCategory.Mouse] = $"https://www.kabum.com.br/perifericos/teclado-mouse{Query}",
            [ComponentCategory.Keyboard] = $"https://www.kabum.com.br/perifericos/teclado-mouse{Query}"
        };

    /// <summary>
    /// Chave de origem por categoria — HardDrive é rastreado separadamente
    /// como "kabum-hd", as demais como "kabum". Precisa bater com o Desktop
    /// (MainWindowViewModel.ImportSource), senão GetLastImportsAsync deixa de
    /// casar entre os dois clientes.
    /// </summary>
    public static string SourceKeyFor(ComponentCategory category) =>
        category == ComponentCategory.HardDrive ? "kabum-hd" : "kabum";

    /// <summary>Chave da URL configurada de uma categoria no servidor.</summary>
    public static string ConfigurationKeyFor(ComponentCategory category) =>
        ImportKeys.SourceUrlKey(category, SourceKeyFor(category));
}
