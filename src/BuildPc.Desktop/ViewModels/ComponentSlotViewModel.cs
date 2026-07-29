using System.Collections.ObjectModel;
using System.Windows.Input;
using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Desktop.ViewModels;

public sealed class ComponentSlotViewModel : ViewModelBase
{
    private readonly Action<ComponentSlotViewModel> _selectionChanged;
    private readonly List<PcComponent> _allOptions;
    private readonly Func<PcComponent, decimal> _priceSelector;
    private PcComponent? _selectedComponent;
    private ComponentOptionViewModel? _selectedOption;
    private ComponentSortOptionViewModel _selectedSortOption;
    private string _filterText = string.Empty;
    private bool _isPickerOpen;
    private int _quantity = 1;

    public ComponentSlotViewModel(
        string slotId,
        ComponentCategory category,
        string title,
        string subtitle,
        string icon,
        IEnumerable<PcComponent> options,
        Action<ComponentSlotViewModel> selectionChanged,
        Func<PcComponent, decimal>? priceSelector = null)
    {
        SlotId = slotId;
        Category = category;
        Title = title;
        Subtitle = subtitle;
        Icon = icon;
        _allOptions = options
            .OrderBy(component => component.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        Options = [];
        SortOptions =
        [
            new("Alfabética: A–Z", ComponentSortMode.NameAscending),
            new("Alfabética: Z–A", ComponentSortMode.NameDescending),
            new("Preço: menor primeiro", ComponentSortMode.PriceAscending),
            new("Preço: maior primeiro", ComponentSortMode.PriceDescending)
        ];
        _selectedSortOption = SortOptions[0];
        _selectionChanged = selectionChanged;
        _priceSelector = priceSelector ?? (component => component.Price);
        TogglePickerCommand = new RelayCommand(() => IsPickerOpen = !IsPickerOpen);
        SortByNameCommand = new RelayCommand(SortByName);
        SortByPriceCommand = new RelayCommand(SortByPrice);
        ApplyFilter();
    }

    public string SlotId { get; }
    public ComponentCategory Category { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string Icon { get; }
    public bool IsAlternate { get; set; }
    public ObservableCollection<ComponentOptionViewModel> Options { get; }
    public IReadOnlyList<int> QuantityOptions { get; } = Enumerable.Range(1, 10).ToList();
    public IReadOnlyList<ComponentSortOptionViewModel> SortOptions { get; }
    public ICommand TogglePickerCommand { get; }
    public ICommand SortByNameCommand { get; }
    public ICommand SortByPriceCommand { get; }

    public ComponentOptionViewModel? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (value is null)
            {
                if (_selectedOption is not null)
                {
                    _selectedOption = null;
                    OnPropertyChanged();
                }
                return;
            }

            SetSelection(value.Component, value, closePicker: true);
        }
    }

    public string SelectedOptionText =>
        Selected?.Name ?? "Selecione uma opção";

    public PcComponent? Selected
    {
        get => _selectedComponent;
        set
        {
            if (value is null)
            {
                SetSelection(null, null, closePicker: false);
                return;
            }

            var matchingOption = Options.FirstOrDefault(option =>
                string.Equals(option.Id, value.Id, StringComparison.OrdinalIgnoreCase));
            if (matchingOption is null)
            {
                _filterText = string.Empty;
                OnPropertyChanged(nameof(FilterText));
                ApplyFilter();
                matchingOption = Options.FirstOrDefault(option =>
                    string.Equals(option.Id, value.Id, StringComparison.OrdinalIgnoreCase));
            }

            SetSelection(value, matchingOption, closePicker: false);
        }
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                ApplyFilter();
            }
        }
    }

    public string FilteredOptionsText =>
        Options.Count == 1 ? "1 opção" : $"{Options.Count} opções";

    public bool IsPickerOpen
    {
        get => _isPickerOpen;
        set => SetProperty(ref _isPickerOpen, value);
    }

    public ComponentSortOptionViewModel SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (value is not null && SetProperty(ref _selectedSortOption, value))
            {
                OnPropertyChanged(nameof(IsNameSortActive));
                OnPropertyChanged(nameof(IsPriceSortActive));
                OnPropertyChanged(nameof(NameSortIcon));
                OnPropertyChanged(nameof(PriceSortIcon));
                ApplyFilter();
            }
        }
    }

    public bool IsNameSortActive =>
        SelectedSortOption.Mode is
            ComponentSortMode.NameAscending or ComponentSortMode.NameDescending;

    public bool IsPriceSortActive =>
        SelectedSortOption.Mode is
            ComponentSortMode.PriceAscending or ComponentSortMode.PriceDescending;

    public string NameSortIcon =>
        SelectedSortOption.Mode == ComponentSortMode.NameDescending
            ? "SortDescending"
            : "SortAscending";

    public string PriceSortIcon =>
        SelectedSortOption.Mode == ComponentSortMode.PriceDescending
            ? "SortDescending"
            : "SortAscending";

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, Math.Clamp(value, 1, 10)))
            {
                OnPropertyChanged(nameof(SelectedPrice));
                _selectionChanged(this);
            }
        }
    }

    public bool HasSelection => Selected is not null;

    public string SelectedPrice => Selected is null
        ? "Não selecionado"
        : (_priceSelector(Selected) * Quantity).ToString(
            "C",
            MainWindowViewModel.BrazilianCulture);

    public void ReplaceOptions(IEnumerable<PcComponent> options)
    {
        _allOptions.Clear();
        _allOptions.AddRange(
            options.OrderBy(component => component.Name, StringComparer.CurrentCultureIgnoreCase));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var selected = Selected;
        var filtered = _allOptions
            .Where(component => ProductFilter.Matches(component, FilterText))
            .ToList();

        filtered = SelectedSortOption.Mode switch
        {
            ComponentSortMode.NameDescending => filtered
                .OrderByDescending(component => component.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(component => component.Id, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ComponentSortMode.PriceAscending => filtered
                .OrderBy(_priceSelector)
                .ThenBy(component => component.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            ComponentSortMode.PriceDescending => filtered
                .OrderByDescending(_priceSelector)
                .ThenBy(component => component.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            _ => filtered
                .OrderBy(component => component.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(component => component.Id, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        Options.Clear();
        for (var index = 0; index < filtered.Count; index++)
        {
            Options.Add(new(
                filtered[index],
                index % 2 == 1,
                FilterText,
                _priceSelector(filtered[index])));
        }

        _selectedOption = selected is null
            ? null
            : Options.FirstOrDefault(option =>
                string.Equals(option.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(SelectedOption));
        OnPropertyChanged(nameof(FilteredOptionsText));
    }

    private void SetSelection(
        PcComponent? component,
        ComponentOptionViewModel? option,
        bool closePicker)
    {
        var componentChanged = !EqualityComparer<PcComponent?>.Default.Equals(
            _selectedComponent,
            component);
        var optionChanged = !ReferenceEquals(_selectedOption, option);

        _selectedComponent = component;
        _selectedOption = option;

        if (optionChanged)
        {
            OnPropertyChanged(nameof(SelectedOption));
        }

        if (!componentChanged)
        {
            if (closePicker)
            {
                IsPickerOpen = false;
            }
            return;
        }

        if (component is null && _quantity != 1)
        {
            _quantity = 1;
            OnPropertyChanged(nameof(Quantity));
        }

        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(SelectedOptionText));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedPrice));
        if (closePicker)
        {
            IsPickerOpen = false;
        }

        _selectionChanged(this);
    }

    private void SortByName()
    {
        var mode = SelectedSortOption.Mode == ComponentSortMode.NameAscending
            ? ComponentSortMode.NameDescending
            : ComponentSortMode.NameAscending;
        SelectedSortOption = SortOptions.Single(option => option.Mode == mode);
    }

    private void SortByPrice()
    {
        var mode = SelectedSortOption.Mode == ComponentSortMode.PriceAscending
            ? ComponentSortMode.PriceDescending
            : ComponentSortMode.PriceAscending;
        SelectedSortOption = SortOptions.Single(option => option.Mode == mode);
    }
}
