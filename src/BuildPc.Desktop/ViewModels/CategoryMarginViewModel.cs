using System.Globalization;
using System.Windows.Input;
using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed class CategoryMarginViewModel : ViewModelBase
{
    private readonly Action<CategoryMarginViewModel> _remove;
    private string _categoryName;
    private string _marginText;

    public CategoryMarginViewModel(
        ComponentCategory category,
        string categoryName,
        decimal margin,
        Action<CategoryMarginViewModel> remove)
    {
        Category = category;
        _categoryName = categoryName;
        _marginText = margin.ToString("N2", MainWindowViewModel.BrazilianCulture);
        _remove = remove;
        RemoveCommand = new RelayCommand(() => _remove(this));
    }

    public ComponentCategory Category { get; }
    public string CategoryName
    {
        get => _categoryName;
        private set => SetProperty(ref _categoryName, value);
    }
    public ICommand RemoveCommand { get; }

    public string MarginText
    {
        get => _marginText;
        set => SetProperty(ref _marginText, value ?? string.Empty);
    }

    public bool TryGetMargin(out decimal margin) =>
        decimal.TryParse(
            MarginText,
            NumberStyles.Number,
            MainWindowViewModel.BrazilianCulture,
        out margin) &&
        margin >= BusinessSettings.MinimumMarginPercent;

    public void SetCategoryName(string name) =>
        CategoryName = name;
}
