using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed record ProductCategoryFilterViewModel(
    ComponentCategory? Value,
    string Name);
