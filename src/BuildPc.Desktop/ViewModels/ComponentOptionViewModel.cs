using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed class ComponentOptionViewModel(
    PcComponent component,
    bool isAlternate,
    string filterText,
    decimal? displayPrice = null)
{
    public PcComponent Component { get; } = component;
    public bool IsAlternate { get; } = isAlternate;
    public string FilterText { get; } = filterText;
    public string Id => Component.Id;
    public string Name => Component.Name;
    public string Brand => Component.Brand;
    public string Description => Component.Description;
    public string? ImageUrl => Component.ImageUrl;
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);
    public string Specifications => string.Join(
        " • ",
        new[] { Component.Socket, Component.MemoryType, Component.FormFactor }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    public bool HasSpecifications => Specifications.Length > 0;

    /// <summary>Favoritos aparecem antes dos demais na lista de seleção.</summary>
    public bool IsFavorite => Component.IsFavorite;
    public decimal Price => displayPrice ?? Component.Price;
    public string DisplayPrice =>
        Price.ToString("C", MainWindowViewModel.BrazilianCulture);
}
