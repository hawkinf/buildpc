using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed record SelectedLineViewModel(
    string Category,
    string Name,
    string Description,
    string? ImageUrl,
    string Price)
{
    public static SelectedLineViewModel From(ComponentSlotViewModel slot, PcComponent component) =>
        new(
            slot.Title,
            $"{component.Name} × {slot.Quantity}",
            component.Description,
            component.ImageUrl,
            (component.Price * slot.Quantity).ToString("C", MainWindowViewModel.BrazilianCulture));
}
