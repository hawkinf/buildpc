namespace BuildPc.Desktop.ViewModels;

public enum ComponentSortMode
{
    NameAscending,
    NameDescending,
    PriceAscending,
    PriceDescending
}

public sealed record ComponentSortOptionViewModel(
    string Name,
    ComponentSortMode Mode);
