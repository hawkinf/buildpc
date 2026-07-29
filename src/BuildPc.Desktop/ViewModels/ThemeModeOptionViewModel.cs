using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed record ThemeModeOptionViewModel(
    AppThemeMode Mode,
    string Name,
    string Description);
