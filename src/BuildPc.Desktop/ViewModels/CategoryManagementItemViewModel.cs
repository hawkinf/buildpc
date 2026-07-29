using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed class CategoryManagementItemViewModel
{
    public CategoryManagementItemViewModel(
        ProductCategoryDefinition definition,
        int productCount,
        bool isAlternate)
    {
        Definition = definition;
        ProductCount = productCount;
        IsAlternate = isAlternate;
    }

    public ProductCategoryDefinition Definition { get; }
    public ComponentCategory Value => Definition.Value;
    public string Name => Definition.Name;
    public bool IsSystem => Definition.IsSystem;
    public bool IsAlternate { get; }
    public int ProductCount { get; }
    public string TypeText => IsSystem ? "Sistema" : "Personalizada";
    public string ProductCountText =>
        ProductCount == 1 ? "1 produto" : $"{ProductCount} produtos";
    public string Icon => Value switch
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
