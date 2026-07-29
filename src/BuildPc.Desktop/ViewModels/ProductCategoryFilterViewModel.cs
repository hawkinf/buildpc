using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed class ProductCategoryFilterViewModel(
    ComponentCategory? value,
    string name) : ViewModelBase
{
    private int _totalCount;
    private int _filteredCount;
    private bool _showFilteredCount;

    public ComponentCategory? Value { get; } = value;
    public string Name { get; } = name;
    public int TotalCount => _totalCount;
    public int FilteredCount => _filteredCount;
    public bool ShowFilteredCount => _showFilteredCount;
    public string DisplayName => ShowFilteredCount
        ? $"{Name} ({TotalCount}) ({FilteredCount})"
        : $"{Name} ({TotalCount})";

    public void UpdateCounts(
        int totalCount,
        int filteredCount,
        bool showFilteredCount)
    {
        if (_totalCount == totalCount &&
            _filteredCount == filteredCount &&
            _showFilteredCount == showFilteredCount)
        {
            return;
        }

        _totalCount = totalCount;
        _filteredCount = filteredCount;
        _showFilteredCount = showFilteredCount;
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(ShowFilteredCount));
        OnPropertyChanged(nameof(DisplayName));
    }
}
