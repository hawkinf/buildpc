using System.Windows.Input;
using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed record CategorySummaryViewModel(
    ComponentCategory Value,
    string Name,
    string Description,
    string Icon,
    int ProductCount,
    bool IsAlternate,
    ICommand ViewProductsCommand)
{
    public string ProductCountText =>
        ProductCount == 1
            ? "1 produto"
            : $"{ProductCount} produtos";

    public static CategorySummaryViewModel From(
        CategoryOptionViewModel category,
        int productCount,
        bool isAlternate,
        Action<ComponentCategory> viewProducts) =>
        new(
            category.Value,
            category.Name,
            DescriptionFor(category.Value),
            IconFor(category.Value),
            productCount,
            isAlternate,
            new RelayCommand(() => viewProducts(category.Value)));

    private static string DescriptionFor(ComponentCategory category) => category switch
    {
        ComponentCategory.Processor => "Processadores AMD e Intel",
        ComponentCategory.Cooler => "Air coolers e water coolers",
        ComponentCategory.Motherboard => "Placas-mãe AMD e Intel",
        ComponentCategory.Memory => "Módulos de memória RAM",
        ComponentCategory.GraphicsCard => "Placas de vídeo dedicadas",
        ComponentCategory.HardDrive => "Discos rígidos internos e externos",
        ComponentCategory.Storage => "Unidades SSD SATA e NVMe",
        ComponentCategory.PowerSupply => "Fontes de alimentação",
        ComponentCategory.Case => "Gabinetes para computadores",
        ComponentCategory.Monitor => "Monitores de vídeo",
        ComponentCategory.Mouse => "Mouses com e sem fio",
        ComponentCategory.Keyboard => "Teclados com e sem fio",
        _ => "Categoria de produtos"
    };

    private static string IconFor(ComponentCategory category) => category switch
    {
        ComponentCategory.Processor => "CPU",
        ComponentCategory.Cooler => "COOL",
        ComponentCategory.Motherboard => "MB",
        ComponentCategory.Memory => "RAM",
        ComponentCategory.GraphicsCard => "GPU",
        ComponentCategory.HardDrive => "Storage",
        ComponentCategory.Storage => "Storage",
        ComponentCategory.PowerSupply => "PSU",
        ComponentCategory.Case => "CASE",
        ComponentCategory.Monitor => "Monitor",
        ComponentCategory.Mouse => "Mouse",
        ComponentCategory.Keyboard => "Keyboard",
        _ => "Components"
    };
}
