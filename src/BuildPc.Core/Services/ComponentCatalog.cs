using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

public static class ComponentCatalog
{
    /// <summary>
    /// Identificadores do catálogo inicial. Só estes precisam de marca de
    /// exclusão para não voltarem a ser semeados no próximo início.
    /// </summary>
    public static IReadOnlySet<string> DefaultIds { get; } =
        CreateDefault()
            .Select(component => component.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PcComponent> CreateDefault() =>
    [
        Item("cpu-7600", ComponentCategory.Processor, "Ryzen 5 7600", "AMD", "6 núcleos • AM5 • 65 W", 1_349.90m, 65, socket: "AM5"),
        Item("cpu-7800x3d", ComponentCategory.Processor, "Ryzen 7 7800X3D", "AMD", "8 núcleos • AM5 • 120 W", 2_699.90m, 120, socket: "AM5"),
        Item("cpu-14600k", ComponentCategory.Processor, "Core i5-14600K", "Intel", "14 núcleos • LGA1700 • 125 W", 2_089.90m, 125, socket: "LGA1700"),

        Item("mb-b650m", ComponentCategory.Motherboard, "B650M Gaming Plus WiFi", "MSI", "mATX • AM5 • DDR5", 1_249.90m, 35, "AM5", "DDR5", "mATX"),
        Item("mb-b650", ComponentCategory.Motherboard, "TUF Gaming B650-Plus", "ASUS", "ATX • AM5 • DDR5", 1_649.90m, 40, "AM5", "DDR5", "ATX"),
        Item("mb-z790", ComponentCategory.Motherboard, "Z790 Aorus Elite AX", "Gigabyte", "ATX • LGA1700 • DDR5", 2_199.90m, 45, "LGA1700", "DDR5", "ATX"),

        Item("ram-16", ComponentCategory.Memory, "Vengeance 16 GB 5600", "Corsair", "2× 8 GB • DDR5 • CL36", 459.90m, 8, memoryType: "DDR5"),
        Item("ram-32", ComponentCategory.Memory, "Fury Beast 32 GB 6000", "Kingston", "2× 16 GB • DDR5 • CL36", 829.90m, 10, memoryType: "DDR5"),
        Item("ram-64", ComponentCategory.Memory, "Trident Z5 64 GB 6000", "G.Skill", "2× 32 GB • DDR5 • CL32", 1_599.90m, 12, memoryType: "DDR5"),

        Item("gpu-4060", ComponentCategory.GraphicsCard, "GeForce RTX 4060 8 GB", "Galax", "8 GB GDDR6 • 115 W", 2_099.90m, 115),
        Item("gpu-7700xt", ComponentCategory.GraphicsCard, "Radeon RX 7700 XT 12 GB", "Sapphire", "12 GB GDDR6 • 245 W", 3_099.90m, 245),
        Item("gpu-4070s", ComponentCategory.GraphicsCard, "GeForce RTX 4070 Super", "ASUS", "12 GB GDDR6X • 220 W", 4_499.90m, 220),

        Item("ssd-1tb", ComponentCategory.Storage, "NV3 NVMe 1 TB", "Kingston", "PCIe 4.0 • 6.000 MB/s", 449.90m, 5),
        Item("ssd-2tb", ComponentCategory.Storage, "990 EVO Plus 2 TB", "Samsung", "PCIe 4.0 • 7.250 MB/s", 999.90m, 6),
        Item("ssd-4tb", ComponentCategory.Storage, "MP600 Pro 4 TB", "Corsair", "PCIe 4.0 • 7.000 MB/s", 2_149.90m, 7),

        Item("psu-550", ComponentCategory.PowerSupply, "CX550 80 Plus Bronze", "Corsair", "550 W • 80 Plus Bronze", 429.90m, 550),
        Item("psu-750", ComponentCategory.PowerSupply, "Core Reactor II 750", "XPG", "750 W • 80 Plus Gold", 769.90m, 750),
        Item("psu-850", ComponentCategory.PowerSupply, "RM850e ATX 3.0", "Corsair", "850 W • 80 Plus Gold", 999.90m, 850),

        Case("case-air", "Air 100 ARGB", "Montech", "mATX • alto fluxo de ar", 399.90m, "mATX", "Mini-ITX"),
        Case("case-flow", "H5 Flow", "NZXT", "ATX • vidro temperado", 649.90m, "ATX", "mATX", "Mini-ITX"),
        Case("case-north", "North Charcoal", "Fractal Design", "ATX • frontal em madeira", 1_199.90m, "ATX", "mATX", "Mini-ITX"),

        Cooler("cooler-ag400", "AG400", "DeepCool", "Torre 120 mm • 220 W TDP", 189.90m, "AM5", "AM4", "LGA1700"),
        Cooler("cooler-ak620", "AK620 Digital", "DeepCool", "Dual tower • display digital", 499.90m, "AM5", "AM4", "LGA1700"),
        Cooler("cooler-aio", "MasterLiquid 360L", "Cooler Master", "Water cooler • radiador 360 mm", 699.90m, "AM5", "AM4", "LGA1700")
    ];

    private static PcComponent Item(
        string id,
        ComponentCategory category,
        string name,
        string brand,
        string description,
        decimal price,
        int powerWatts,
        string? socket = null,
        string? memoryType = null,
        string? formFactor = null) =>
        new()
        {
            Id = id,
            Category = category,
            Name = name,
            Brand = brand,
            Description = description,
            Price = price,
            PowerWatts = powerWatts,
            Socket = socket,
            MemoryType = memoryType,
            FormFactor = formFactor
        };

    private static PcComponent Case(
        string id,
        string name,
        string brand,
        string description,
        decimal price,
        params string[] supportedFormFactors) =>
        Item(id, ComponentCategory.Case, name, brand, description, price, 5) with
        {
            SupportedFormFactors = new HashSet<string>(supportedFormFactors, StringComparer.OrdinalIgnoreCase)
        };

    private static PcComponent Cooler(
        string id,
        string name,
        string brand,
        string description,
        decimal price,
        params string[] supportedSockets) =>
        Item(id, ComponentCategory.Cooler, name, brand, description, price, 8) with
        {
            SupportedSockets = new HashSet<string>(supportedSockets, StringComparer.OrdinalIgnoreCase)
        };
}
