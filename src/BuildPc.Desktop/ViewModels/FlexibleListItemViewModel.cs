using System.Windows.Input;
using System.Globalization;
using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed class FlexibleListItemViewModel : ViewModelBase
{
    private readonly Action<FlexibleListItemViewModel> _remove;
    private readonly Action _changed;
    private readonly Action<FlexibleListItemViewModel, int>? _move;
    private bool _canMoveUp;
    private bool _canMoveDown;
    private int _quantity;
    private bool _isAlternate;
    private string _name;
    private string _categoryName;
    private string _description;
    private string _priceText;
    private decimal _unitPriceValue;
    private decimal _marginPercent;
    private string _sellingPriceText;
    private decimal _sellingUnitPriceValue;
    private bool _isCostVisible;

    public FlexibleListItemViewModel(
        PcComponent component,
        string categoryName,
        int quantity,
        decimal marginPercent,
        Action<FlexibleListItemViewModel> remove,
        Action changed,
        Action<FlexibleListItemViewModel, int>? move = null)
    {
        Component = component;
        _categoryName = categoryName;
        _quantity = QuantityRange.Clamp(quantity);
        _name = component.Name;
        _description = component.Description;
        _unitPriceValue = component.Price;
        _marginPercent = marginPercent;
        _priceText = component.Price.ToString("N2", MainWindowViewModel.BrazilianCulture);
        _sellingUnitPriceValue = CalculateSalePrice(component.Price, marginPercent);
        _sellingPriceText = _sellingUnitPriceValue.ToString(
            "N2",
            MainWindowViewModel.BrazilianCulture);
        _remove = remove;
        _changed = changed;
        _move = move;
        RemoveCommand = new RelayCommand(() => _remove(this));
        MoveUpCommand = new RelayCommand(() => _move?.Invoke(this, -1));
        MoveDownCommand = new RelayCommand(() => _move?.Invoke(this, 1));
    }

    /// <summary>
    /// Recria uma linha a partir de um orçamento gravado. Preserva título,
    /// descrição, custo, margem e preço de venda como foram acordados, em vez
    /// de recalcular pelo catálogo atual, que pode ter mudado desde então.
    /// </summary>
    public FlexibleListItemViewModel(
        PcComponent component,
        SavedQuoteItem savedItem,
        string categoryName,
        Action<FlexibleListItemViewModel> remove,
        Action changed,
        Action<FlexibleListItemViewModel, int>? move = null)
        : this(
            component,
            categoryName,
            savedItem.Quantity,
            savedItem.MarginPercent,
            remove,
            changed,
            move)
    {
        _name = savedItem.Name;
        _description = savedItem.Description;
        _unitPriceValue = savedItem.UnitCost;
        _priceText = savedItem.UnitCost.ToString(
            "N2",
            MainWindowViewModel.BrazilianCulture);
        _sellingUnitPriceValue = savedItem.UnitPrice;
        _sellingPriceText = savedItem.UnitPrice.ToString(
            "N2",
            MainWindowViewModel.BrazilianCulture);
    }

    public PcComponent Component { get; }
    public string CategoryName
    {
        get => _categoryName;
        private set => SetProperty(ref _categoryName, value);
    }
    public string Brand => Component.Brand;
    public string? ImageUrl => Component.ImageUrl;
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);
    public string Icon => Component.Category switch
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
        _ => "Products"
    };

    public ICommand RemoveCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }

    /// <summary>
    /// A ordem das linhas é a ordem dos itens no PDF do cliente; os botões das
    /// pontas ficam desabilitados.
    /// </summary>
    public bool CanMoveUp
    {
        get => _canMoveUp;
        private set => SetProperty(ref _canMoveUp, value);
    }

    public bool CanMoveDown
    {
        get => _canMoveDown;
        private set => SetProperty(ref _canMoveDown, value);
    }

    public void SetMovability(bool canMoveUp, bool canMoveDown)
    {
        CanMoveUp = canMoveUp;
        CanMoveDown = canMoveDown;
    }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value ?? string.Empty))
            {
                _changed();
            }
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value ?? string.Empty))
            {
                _changed();
            }
        }
    }

    public string PriceText
    {
        get => _priceText;
        set
        {
            if (!SetProperty(ref _priceText, value ?? string.Empty))
            {
                return;
            }

            if (decimal.TryParse(
                    _priceText,
                    NumberStyles.Currency,
                    MainWindowViewModel.BrazilianCulture,
                    out var price) &&
                price >= 0)
            {
                _unitPriceValue = price;
                RecalculateSellingPrice();
                OnPropertyChanged(nameof(UnitPriceValue));
                OnPropertyChanged(nameof(CostUnitPrice));
                OnPropertyChanged(nameof(TotalPrice));
                _changed();
            }
        }
    }

    public decimal UnitPriceValue => _unitPriceValue;
    public string CostUnitPrice =>
        UnitPriceValue.ToString("C", MainWindowViewModel.BrazilianCulture);
    public decimal MarginPercent => _marginPercent;
    public string MarginText =>
        $"{MarginPercent.ToString("N2", MainWindowViewModel.BrazilianCulture)}%";
    public decimal SellingUnitPriceValue => _sellingUnitPriceValue;
    public string SellingUnitPrice =>
        SellingUnitPriceValue.ToString("C", MainWindowViewModel.BrazilianCulture);

    public string SellingPriceText
    {
        get => _sellingPriceText;
        set
        {
            if (!SetProperty(ref _sellingPriceText, value ?? string.Empty))
            {
                return;
            }

            if (!decimal.TryParse(
                    _sellingPriceText,
                    NumberStyles.Currency,
                    MainWindowViewModel.BrazilianCulture,
                    out var price) ||
                price < 0)
            {
                _sellingPriceText = _sellingUnitPriceValue.ToString(
                    "N2",
                    MainWindowViewModel.BrazilianCulture);
                OnPropertyChanged(nameof(SellingPriceText));
                return;
            }

            _sellingUnitPriceValue = Math.Max(
                MinimumSellingUnitPrice,
                RoundUpToNinetyCents(price));
            _sellingPriceText = _sellingUnitPriceValue.ToString(
                "N2",
                MainWindowViewModel.BrazilianCulture);
            OnPropertyChanged(nameof(SellingPriceText));

            _marginPercent = UnitPriceValue > 0
                ? decimal.Round(
                    (_sellingUnitPriceValue / UnitPriceValue - 1m) * 100m,
                    4,
                    MidpointRounding.AwayFromZero)
                : BusinessSettings.MinimumMarginPercent;
            OnPropertyChanged(nameof(SellingUnitPriceValue));
            OnPropertyChanged(nameof(SellingUnitPrice));
            OnPropertyChanged(nameof(MarginPercent));
            OnPropertyChanged(nameof(MarginText));
            OnPropertyChanged(nameof(TotalPrice));
            _changed();
        }
    }

    public bool IsAlternate
    {
        get => _isAlternate;
        set => SetProperty(ref _isAlternate, value);
    }

    public decimal MinimumSellingUnitPrice =>
        CalculateSalePrice(
            UnitPriceValue,
            BusinessSettings.MinimumMarginPercent);

    public bool IsCostVisible
    {
        get => _isCostVisible;
        private set => SetProperty(ref _isCostVisible, value);
    }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, QuantityRange.Clamp(value)))
            {
                OnPropertyChanged(nameof(TotalPrice));
                _changed();
            }
        }
    }

    public string TotalPrice =>
        (SellingUnitPriceValue * Quantity).ToString(
            "C",
            MainWindowViewModel.BrazilianCulture);

    public void ApplyMargin(decimal marginPercent)
    {
        if (_marginPercent == marginPercent)
        {
            return;
        }

        _marginPercent = marginPercent;
        RecalculateSellingPrice();
        OnPropertyChanged(nameof(MarginPercent));
        OnPropertyChanged(nameof(MarginText));
        OnPropertyChanged(nameof(TotalPrice));
    }

    public void SetCostVisible(bool visible) => IsCostVisible = visible;

    public void SetCategoryName(string name) =>
        CategoryName = name;

    private void RecalculateSellingPrice()
    {
        _sellingUnitPriceValue = CalculateSalePrice(
            UnitPriceValue,
            MarginPercent);
        _sellingPriceText = _sellingUnitPriceValue.ToString(
            "N2",
            MainWindowViewModel.BrazilianCulture);
        OnPropertyChanged(nameof(SellingPriceText));
        OnPropertyChanged(nameof(SellingUnitPriceValue));
        OnPropertyChanged(nameof(SellingUnitPrice));
    }

    public static decimal CalculateSalePrice(decimal cost, decimal margin) =>
        RoundUpToNinetyCents(
            cost * (1m + Math.Max(BusinessSettings.MinimumMarginPercent, margin) / 100m));

    private static decimal RoundUpToNinetyCents(decimal value)
    {
        if (value <= 0)
        {
            return 0.90m;
        }

        var wholePart = decimal.Floor(value);
        var priceEndingInNinety = wholePart + 0.90m;
        return value <= priceEndingInNinety
            ? priceEndingInNinety
            : wholePart + 1.90m;
    }
}
