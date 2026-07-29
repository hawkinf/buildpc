using System.Windows.Input;
using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed class ProductListItemViewModel : ViewModelBase
{
    private readonly Func<ProductListItemViewModel, bool> _toggleKeep;
    private readonly Action<ProductListItemViewModel> _select;
    private readonly Action _bulkSelectionChanged;
    private bool _isKept;
    private bool _isSelected;
    private bool _isBulkSelected;
    private bool _isAlternate;

    private ProductListItemViewModel(
        PcComponent component,
        Func<ProductListItemViewModel, bool> toggleKeep,
        Action<ProductListItemViewModel> select,
        Action bulkSelectionChanged,
        bool isAlternate,
        string? categoryName = null)
    {
        Component = component;
        Id = component.Id;
        Name = component.Name;
        Brand = component.Brand;
        Category = categoryName ?? CategoryName(component.Category);
        Icon = CategoryIcon(component.Category);
        Description = component.Description;
        ImageUrl = component.ImageUrl;
        Price = component.Price.ToString("C", MainWindowViewModel.BrazilianCulture);
        PowerText = component.PowerWatts > 0 ? $"{component.PowerWatts} W" : "Não informado";
        SocketText = ValueOrNotInformed(component.Socket);
        MemoryTypeText = ValueOrNotInformed(component.MemoryType);
        FormFactorText = ValueOrNotInformed(component.FormFactor);
        SupportedSocketsText = SetOrNotInformed(component.SupportedSockets);
        SupportedFormFactorsText = SetOrNotInformed(component.SupportedFormFactors);
        IsImported = !string.IsNullOrWhiteSpace(component.ImportSource);
        _isAlternate = isAlternate;
        OriginText = IsImported
            ? "Importado"
            : component.IsUserDefined
                ? "Manual"
                : "Incluído";
        _isKept = component.KeepOnImport;
        _toggleKeep = toggleKeep;
        _select = select;
        _bulkSelectionChanged = bulkSelectionChanged;
        ToggleKeepCommand = new RelayCommand(ToggleKeep);
        SelectCommand = new RelayCommand(() => _select(this));
    }

    public PcComponent Component { get; }
    public string Id { get; }
    public string Name { get; }
    public string Brand { get; }
    public string Category { get; }
    public string Icon { get; }
    public string Description { get; }
    public string? ImageUrl { get; }
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);
    public string Price { get; }
    public string PowerText { get; }
    public string SocketText { get; }
    public string MemoryTypeText { get; }
    public string FormFactorText { get; }
    public string SupportedSocketsText { get; }
    public string SupportedFormFactorsText { get; }
    public string OriginText { get; }
    public bool IsImported { get; }
    public bool IsAlternate
    {
        get => _isAlternate;
        private set => SetProperty(ref _isAlternate, value);
    }
    public ICommand ToggleKeepCommand { get; }
    public ICommand SelectCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }

    public bool IsBulkSelected
    {
        get => _isBulkSelected;
        set
        {
            if (SetProperty(ref _isBulkSelected, value))
            {
                _bulkSelectionChanged();
            }
        }
    }

    public bool IsKept
    {
        get => _isKept;
        private set
        {
            if (SetProperty(ref _isKept, value))
            {
                OnPropertyChanged(nameof(KeepButtonText));
                OnPropertyChanged(nameof(KeepIcon));
            }
        }
    }

    public string KeepButtonText => IsKept ? "Mantido" : "Manter";
    public string KeepIcon => IsKept ? "Check" : "Pin";

    public void SetAlternate(bool isAlternate) => IsAlternate = isAlternate;

    public static ProductListItemViewModel From(
        PcComponent component,
        Func<ProductListItemViewModel, bool> toggleKeep,
        Action<ProductListItemViewModel> select,
        Action bulkSelectionChanged,
        bool isAlternate) =>
        new(component, toggleKeep, select, bulkSelectionChanged, isAlternate);

    public static ProductListItemViewModel From(
        PcComponent component,
        string categoryName,
        Func<ProductListItemViewModel, bool> toggleKeep,
        Action<ProductListItemViewModel> select,
        Action bulkSelectionChanged,
        bool isAlternate) =>
        new(
            component,
            toggleKeep,
            select,
            bulkSelectionChanged,
            isAlternate,
            categoryName);

    public static ProductListItemViewModel From(
        PcComponent component,
        Func<ProductListItemViewModel, bool> toggleKeep,
        Action<ProductListItemViewModel> select,
        bool isAlternate) =>
        new(component, toggleKeep, select, () => { }, isAlternate);

    private void ToggleKeep()
    {
        if (_toggleKeep(this))
        {
            IsKept = !IsKept;
        }
    }

    private static string CategoryName(ComponentCategory category) => category switch
    {
        ComponentCategory.Processor => "Processador",
        ComponentCategory.Motherboard => "Placa-mãe",
        ComponentCategory.Memory => "Memória",
        ComponentCategory.GraphicsCard => "Placa de vídeo",
        ComponentCategory.HardDrive => "Disco rígido (HD)",
        ComponentCategory.Storage => "SSD / NVMe",
        ComponentCategory.PowerSupply => "Fonte",
        ComponentCategory.Case => "Gabinete",
        ComponentCategory.Cooler => "Cooler",
        ComponentCategory.Monitor => "Monitor",
        ComponentCategory.Mouse => "Mouse",
        ComponentCategory.Keyboard => "Teclado",
        _ => category.ToString()
    };

    private static string CategoryIcon(ComponentCategory category) => category switch
    {
        ComponentCategory.Processor => "CPU",
        ComponentCategory.Motherboard => "MB",
        ComponentCategory.Memory => "RAM",
        ComponentCategory.GraphicsCard => "GPU",
        ComponentCategory.HardDrive => "Storage",
        ComponentCategory.Storage => "Storage",
        ComponentCategory.PowerSupply => "PSU",
        ComponentCategory.Case => "CASE",
        ComponentCategory.Cooler => "COOL",
        ComponentCategory.Monitor => "Monitor",
        ComponentCategory.Mouse => "Mouse",
        ComponentCategory.Keyboard => "Keyboard",
        _ => "Components"
    };

    private static string ValueOrNotInformed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Não informado" : value;

    private static string SetOrNotInformed(IEnumerable<string> values)
    {
        var items = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        return items.Count == 0 ? "Não informado" : string.Join(", ", items);
    }
}
