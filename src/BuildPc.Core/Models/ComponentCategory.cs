namespace BuildPc.Core.Models;

public enum ComponentCategory
{
    Processor = 0,
    Motherboard = 1,
    Memory = 2,
    GraphicsCard = 3,
    Storage = 4,
    PowerSupply = 5,
    Case = 6,
    Cooler = 7,
    HardDrive = 8,
    Monitor = 9,
    Mouse = 10,
    Keyboard = 11
}

public static class ComponentCategoryInfo
{
    public static int DisplayOrder(ComponentCategory category) => category switch
    {
        ComponentCategory.Processor => 0,
        ComponentCategory.Cooler => 1,
        ComponentCategory.Motherboard => 2,
        ComponentCategory.Memory => 3,
        ComponentCategory.GraphicsCard => 4,
        ComponentCategory.HardDrive => 5,
        ComponentCategory.Storage => 6,
        ComponentCategory.PowerSupply => 7,
        ComponentCategory.Case => 8,
        ComponentCategory.Monitor => 9,
        ComponentCategory.Mouse => 10,
        ComponentCategory.Keyboard => 11,
        _ => int.MaxValue
    };
}
