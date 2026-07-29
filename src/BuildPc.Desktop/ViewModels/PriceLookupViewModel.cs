using System.Collections.ObjectModel;
using System.Windows.Input;
using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Desktop.ViewModels;

public sealed class PriceLookupViewModel : ViewModelBase
{
    private readonly List<PriceLookupItemViewModel> _products = [];
    private BusinessSettings _settings;
    private ProductCategoryFilterViewModel _selectedCategory = null!;
    private ProductCatalogSortMode _sortMode =
        ProductCatalogSortMode.DescriptionAscending;
    private bool _showSalePrice = true;
    private string _searchText = string.Empty;

    public PriceLookupViewModel(
        IEnumerable<PcComponent> catalog,
        IReadOnlyList<ProductCategoryDefinition> categories,
        BusinessSettings settings)
    {
        _settings = settings;
        Categories = [];
        Items = [];
        ShowCostCommand = new RelayCommand(() => SetPriceMode(false));
        ShowSaleCommand = new RelayCommand(() => SetPriceMode(true));
        SortByTitleCommand = new RelayCommand(SortByTitle);
        SortByPriceCommand = new RelayCommand(SortByPrice);
        UpdateCategories(categories);
        UpdateCatalog(catalog);
    }

    public ObservableCollection<ProductCategoryFilterViewModel> Categories { get; }
    public ObservableCollection<PriceLookupItemViewModel> Items { get; }
    public ICommand ShowCostCommand { get; }
    public ICommand ShowSaleCommand { get; }
    public ICommand SortByTitleCommand { get; }
    public ICommand SortByPriceCommand { get; }

    public ProductCategoryFilterViewModel SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (value is not null && SetProperty(ref _selectedCategory, value))
            {
                RefreshItems();
            }
        }
    }

    public bool IsCostActive => !_showSalePrice;
    public bool IsSaleActive => _showSalePrice;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                RefreshItems();
            }
        }
    }
    public string PriceColumnTitle =>
        _showSalePrice ? "PREÇO DE VENDA" : "CUSTO";
    public bool IsTitleSortActive =>
        _sortMode is
            ProductCatalogSortMode.DescriptionAscending or
            ProductCatalogSortMode.DescriptionDescending;
    public bool IsPriceSortActive =>
        _sortMode is
            ProductCatalogSortMode.PriceAscending or
            ProductCatalogSortMode.PriceDescending;
    public string TitleSortIcon =>
        _sortMode == ProductCatalogSortMode.DescriptionDescending
            ? "SortDescending"
            : "SortAscending";
    public string PriceSortIcon =>
        _sortMode == ProductCatalogSortMode.PriceDescending
            ? "SortDescending"
            : "SortAscending";
    public string CountText =>
        Items.Count == 1 ? "1 produto" : $"{Items.Count} produtos";

    public void UpdateCatalog(IEnumerable<PcComponent> catalog)
    {
        var categoryNames = Categories
            .Where(category => category.Value is not null)
            .ToDictionary(category => category.Value!.Value, category => category.Name);
        _products.Clear();
        _products.AddRange(catalog.Select(component =>
            new PriceLookupItemViewModel(
                component,
                categoryNames.GetValueOrDefault(
                    component.Category,
                    component.Category.ToString()))));
        UpdateDisplayedPrices();
        RefreshItems();
    }

    public void UpdateCategories(
        IReadOnlyList<ProductCategoryDefinition> categories)
    {
        var selectedCategory = Categories.Count == 0
            ? null
            : SelectedCategory.Value;
        Categories.Clear();
        Categories.Add(new ProductCategoryFilterViewModel(null, "Todos"));
        foreach (var category in categories
                     .OrderBy(category => category.DisplayOrder)
                     .ThenBy(
                         category => category.Name,
                         StringComparer.CurrentCultureIgnoreCase))
        {
            Categories.Add(new ProductCategoryFilterViewModel(
                category.Value,
                category.Name));
        }

        _selectedCategory =
            Categories.FirstOrDefault(category =>
                category.Value == selectedCategory) ??
            Categories[0];
        OnPropertyChanged(nameof(SelectedCategory));

        if (_products.Count > 0)
        {
            UpdateCatalog(_products.Select(product => product.Component).ToList());
        }
    }

    public void ApplySettings(BusinessSettings settings)
    {
        _settings = settings;
        UpdateDisplayedPrices();
        RefreshItems();
    }

    private void SetPriceMode(bool showSalePrice)
    {
        if (_showSalePrice == showSalePrice)
        {
            return;
        }

        _showSalePrice = showSalePrice;
        OnPropertyChanged(nameof(IsCostActive));
        OnPropertyChanged(nameof(IsSaleActive));
        OnPropertyChanged(nameof(PriceColumnTitle));
        UpdateDisplayedPrices();
        RefreshItems();
    }

    private void SortByTitle()
    {
        _sortMode = _sortMode == ProductCatalogSortMode.DescriptionAscending
            ? ProductCatalogSortMode.DescriptionDescending
            : ProductCatalogSortMode.DescriptionAscending;
        NotifySortingChanged();
    }

    private void SortByPrice()
    {
        _sortMode = _sortMode == ProductCatalogSortMode.PriceAscending
            ? ProductCatalogSortMode.PriceDescending
            : ProductCatalogSortMode.PriceAscending;
        NotifySortingChanged();
    }

    private void NotifySortingChanged()
    {
        OnPropertyChanged(nameof(IsTitleSortActive));
        OnPropertyChanged(nameof(IsPriceSortActive));
        OnPropertyChanged(nameof(TitleSortIcon));
        OnPropertyChanged(nameof(PriceSortIcon));
        RefreshItems();
    }

    private void UpdateDisplayedPrices()
    {
        foreach (var product in _products)
        {
            var price = _showSalePrice
                ? FlexibleListItemViewModel.CalculateSalePrice(
                    product.Component.Price,
                    _settings.MarginFor(product.Component.Category))
                : product.Component.Price;
            product.SetDisplayPrice(price);
        }
    }

    private void RefreshItems()
    {
        var filtered = _products.Where(product =>
            (SelectedCategory.Value is null ||
             product.Component.Category == SelectedCategory.Value) &&
            ProductFilter.Matches(product.Component, SearchText));
        var sorted = _sortMode switch
        {
            ProductCatalogSortMode.DescriptionDescending => filtered
                .OrderByDescending(
                    product => product.Name,
                    StringComparer.CurrentCultureIgnoreCase),
            ProductCatalogSortMode.PriceAscending => filtered
                .OrderBy(product => product.DisplayPriceValue)
                .ThenBy(
                    product => product.Name,
                    StringComparer.CurrentCultureIgnoreCase),
            ProductCatalogSortMode.PriceDescending => filtered
                .OrderByDescending(product => product.DisplayPriceValue)
                .ThenBy(
                    product => product.Name,
                    StringComparer.CurrentCultureIgnoreCase),
            _ => filtered.OrderBy(
                product => product.Name,
                StringComparer.CurrentCultureIgnoreCase)
        };

        Items.Clear();
        var index = 0;
        foreach (var product in sorted)
        {
            product.SetAlternate(index++ % 2 == 1);
            Items.Add(product);
        }

        RefreshCategoryCounts();
        OnPropertyChanged(nameof(CountText));
    }

    private void RefreshCategoryCounts()
    {
        var showFilteredCount = !string.IsNullOrWhiteSpace(SearchText);
        foreach (var category in Categories)
        {
            var productsInCategory = category.Value is null
                ? _products.AsEnumerable()
                : _products.Where(product =>
                    product.Component.Category == category.Value);
            var totalCount = productsInCategory.Count();
            var filteredCount = showFilteredCount
                ? productsInCategory.Count(product =>
                    ProductFilter.Matches(
                        product.Component,
                        SearchText))
                : totalCount;
            category.UpdateCounts(
                totalCount,
                filteredCount,
                showFilteredCount);
        }
    }
}

public sealed class PriceLookupItemViewModel : ViewModelBase
{
    private decimal _displayPriceValue;
    private string _displayPrice = string.Empty;
    private bool _isAlternate;

    public PriceLookupItemViewModel(
        PcComponent component,
        string categoryName)
    {
        Component = component;
        Name = component.Name;
        Brand = component.Brand;
        Category = categoryName;
        Description = component.Description;
        ImageUrl = component.ImageUrl;
    }

    public PcComponent Component { get; }
    public string Name { get; }
    public string Brand { get; }
    public string Category { get; }
    public string Description { get; }
    public string? ImageUrl { get; }
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);
    public decimal DisplayPriceValue
    {
        get => _displayPriceValue;
        private set => SetProperty(ref _displayPriceValue, value);
    }
    public string DisplayPrice
    {
        get => _displayPrice;
        private set => SetProperty(ref _displayPrice, value);
    }
    public bool IsAlternate
    {
        get => _isAlternate;
        private set => SetProperty(ref _isAlternate, value);
    }
    public void SetDisplayPrice(decimal price)
    {
        DisplayPriceValue = price;
        DisplayPrice = price.ToString(
            "C",
            MainWindowViewModel.BrazilianCulture);
    }

    public void SetAlternate(bool isAlternate) => IsAlternate = isAlternate;
}
