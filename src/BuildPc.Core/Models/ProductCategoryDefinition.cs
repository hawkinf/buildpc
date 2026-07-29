namespace BuildPc.Core.Models;

public sealed record ProductCategoryDefinition
{
    public required ComponentCategory Value { get; init; }
    public required string Name { get; init; }
    public required int DisplayOrder { get; init; }
    public bool IsSystem { get; init; }

    public static IReadOnlyList<ProductCategoryDefinition> Defaults() =>
    [
        System(ComponentCategory.Processor, "Processador"),
        System(ComponentCategory.Cooler, "Coolers"),
        System(ComponentCategory.Motherboard, "Placa-mãe"),
        System(ComponentCategory.Memory, "Memória"),
        System(ComponentCategory.GraphicsCard, "Placa de vídeo"),
        System(ComponentCategory.HardDrive, "Discos rígidos (HD)"),
        System(ComponentCategory.Storage, "SSD / NVMe"),
        System(ComponentCategory.PowerSupply, "Fonte"),
        System(ComponentCategory.Case, "Gabinete"),
        System(ComponentCategory.Monitor, "Monitores"),
        System(ComponentCategory.Mouse, "Mouses"),
        System(ComponentCategory.Keyboard, "Teclados")
    ];

    private static ProductCategoryDefinition System(
        ComponentCategory value,
        string name) =>
        new()
        {
            Value = value,
            Name = name,
            DisplayOrder = ComponentCategoryInfo.DisplayOrder(value),
            IsSystem = true
        };
}
