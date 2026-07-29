namespace BuildPc.Core.Models;

public sealed class PcBuild
{
    private readonly Dictionary<string, BuildSelection> _selections =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<ComponentCategory, PcComponent> Components =>
        _selections.Values
            .GroupBy(selection => selection.Component.Category)
            .ToDictionary(group => group.Key, group => group.First().Component);

    public decimal TotalCost =>
        _selections.Values.Sum(selection => selection.Component.Price * selection.Quantity);

    public int EstimatedPowerWatts =>
        _selections.Values
            .Where(selection => selection.Component.Category != ComponentCategory.PowerSupply)
            .Sum(selection => selection.Component.PowerWatts * selection.Quantity) +
        (_selections.Count > 0 ? 90 : 0);

    public int CompletedSlots => _selections.Count;

    public void Select(PcComponent? component, ComponentCategory category) =>
        Select(component, category, category.ToString(), 1);

    public void Select(
        PcComponent? component,
        ComponentCategory category,
        string slotId,
        int quantity)
    {
        if (component is null)
        {
            _selections.Remove(slotId);
            return;
        }

        if (component.Category != category)
        {
            throw new ArgumentException("O componente não pertence à categoria informada.", nameof(component));
        }

        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "A quantidade deve ser maior que zero.");
        }

        _selections[slotId] = new(component, quantity);
    }

    public PcComponent? Get(ComponentCategory category) =>
        _selections.Values
            .FirstOrDefault(selection => selection.Component.Category == category)
            ?.Component;

    public void Clear() => _selections.Clear();

    private sealed record BuildSelection(PcComponent Component, int Quantity);
}
