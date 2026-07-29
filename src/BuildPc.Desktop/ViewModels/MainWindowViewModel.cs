using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;
using BuildPc.Core.Models;
using BuildPc.Core.Services;
using BuildPc.Desktop.Services;
using Microsoft.Data.Sqlite;

namespace BuildPc.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private const int TotalSlots = 12;
    private readonly PcBuild _build = new();
    private readonly CompatibilityService _compatibilityService = new();
    private readonly IComponentCatalogRepository _catalogRepository;
    private readonly KabumCatalogImporter _kabumCatalogImporter;
    private readonly IQuoteRepository _quoteRepository;
    private readonly string _productImagesDirectory;
    private BusinessSettings _businessSettings;
    private string _currentView = "flexible-list";
    private CategoryOptionViewModel? _selectedProductCategory;
    private string _productName = string.Empty;
    private string _productBrand = string.Empty;
    private string _productDescription = string.Empty;
    private string _productPrice = string.Empty;
    private string _productPower = string.Empty;
    private string _productSocket = string.Empty;
    private string _productMemoryType = string.Empty;
    private string _productFormFactor = string.Empty;
    private string _supportedSockets = string.Empty;
    private string _supportedFormFactors = string.Empty;
    private string _productFormMessage = string.Empty;
    private bool _isProductFormSuccess;
    private bool _isImportingAll;
    private ProductListItemViewModel? _selectedCatalogProduct;
    private string? _editingProductId;
    private bool _isDeleteConfirmationVisible;
    private string _bulkDescriptionText = string.Empty;
    private ProductDescriptionOperationViewModel _selectedBulkDescriptionOperation = null!;
    private string _bulkStatusMessage = string.Empty;
    private ProductCategoryFilterViewModel _selectedCatalogCategoryFilter = null!;
    private bool _isBulkDeleteConfirmationVisible;
    private string _catalogSearchText = string.Empty;
    private ProductCatalogSortOptionViewModel _selectedCatalogSort = null!;
    private string _productImagePath = string.Empty;
    private ProductPriceTableOptionViewModel _selectedProductPriceTableOption = null!;
    private bool _isExportingProductPriceTable;
    private string _productPriceTableStatusMessage = string.Empty;

    public static CultureInfo BrazilianCulture { get; } = CultureInfo.GetCultureInfo("pt-BR");

    public MainWindowViewModel()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BuildPC");
        var databasePath = Path.Combine(dataDirectory, "catalogo.db");
        var legacyJsonPath = Path.Combine(dataDirectory, "produtos.json");
        _productImagesDirectory = Path.Combine(dataDirectory, "imagens-produtos");
        var apiSettingsPath = Path.Combine(dataDirectory, "servidor.json");
        var apiSettings = BuildPcApiSettings.Load(apiSettingsPath);
        if (apiSettings is not null)
        {
            var apiClient = new BuildPcApiClient(apiSettings);
            _catalogRepository = apiClient;
            _quoteRepository = apiClient;
        }
        else
        {
            _catalogRepository = new ComponentCatalogRepository(databasePath, legacyJsonPath);
            _quoteRepository = new QuoteRepository(databasePath);
        }
        _businessSettings = _quoteRepository.GetSettings();
        _kabumCatalogImporter = new KabumCatalogImporter(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45)
        });
        var catalog = _catalogRepository.GetAll();
        Slots =
        [
            Slot("processor", ComponentCategory.Processor, "Processador", "O cérebro da máquina", "CPU", catalog),
            Slot("motherboard", ComponentCategory.Motherboard, "Placa-mãe", "Conecta todos os componentes", "MB", catalog),
            Slot("memory", ComponentCategory.Memory, "Memória", "Velocidade para multitarefa", "RAM", catalog),
            Slot("graphics", ComponentCategory.GraphicsCard, "Placa de vídeo", "Desempenho gráfico", "GPU", catalog),
            Slot("storage-primary", ComponentCategory.Storage, "Armazenamento 1", "SSD SATA ou NVMe principal", "Storage", catalog),
            Slot("storage-secondary", ComponentCategory.Storage, "Armazenamento 2", "SSD SATA ou NVMe adicional", "Storage", catalog),
            Slot("power-supply", ComponentCategory.PowerSupply, "Fonte", "Energia estável e segura", "PSU", catalog),
            Slot("case", ComponentCategory.Case, "Gabinete", "Espaço, ventilação e estilo", "CASE", catalog),
            Slot("cooler", ComponentCategory.Cooler, "Cooler", "Mantém a temperatura sob controle", "COOL", catalog),
            Slot("monitor", ComponentCategory.Monitor, "Monitor", "A imagem da configuração", "Monitor", catalog),
            Slot("mouse", ComponentCategory.Mouse, "Mouse", "Controle e precisão", "Mouse", catalog),
            Slot("keyboard", ComponentCategory.Keyboard, "Teclado", "Comandos e produtividade", "Keyboard", catalog)
        ];
        for (var index = 0; index < Slots.Count; index++)
        {
            Slots[index].IsAlternate = index % 2 == 1;
        }

        SelectedItems = [];
        Issues = [];
        Products = new ObservableCollection<ProductListItemViewModel>(
            catalog.Select((component, index) =>
                ProductListItemViewModel.From(
                    component,
                    ToggleKeep,
                    SelectCatalogProduct,
                    BulkSelectionChanged,
                    index % 2 == 1)));
        CategoryOptions =
        [
            new(ComponentCategory.Processor, "Processador"),
            new(ComponentCategory.Cooler, "Coolers"),
            new(ComponentCategory.Motherboard, "Placa-mãe"),
            new(ComponentCategory.Memory, "Memória"),
            new(ComponentCategory.GraphicsCard, "Placa de vídeo"),
            new(ComponentCategory.HardDrive, "Discos rígidos (HD)"),
            new(ComponentCategory.Storage, "SSD / NVMe"),
            new(ComponentCategory.PowerSupply, "Fonte"),
            new(ComponentCategory.Case, "Gabinete"),
            new(ComponentCategory.Monitor, "Monitores"),
            new(ComponentCategory.Mouse, "Mouses"),
            new(ComponentCategory.Keyboard, "Teclados")
        ];
        ProductCategoryFilters =
        [
            new(null, "Todos"),
            .. CategoryOptions.Select(category =>
                new ProductCategoryFilterViewModel(category.Value, category.Name))
        ];
        _selectedCatalogCategoryFilter = ProductCategoryFilters[0];
        CatalogSortOptions =
        [
            new("Descrição: A–Z", ProductCatalogSortMode.DescriptionAscending),
            new("Descrição: Z–A", ProductCatalogSortMode.DescriptionDescending),
            new("Custo: menor primeiro", ProductCatalogSortMode.PriceAscending),
            new("Custo: maior primeiro", ProductCatalogSortMode.PriceDescending)
        ];
        _selectedCatalogSort = CatalogSortOptions[0];
        ProductPriceTableOptions =
        [
            new(ProductPriceTableKind.Cost, "Tabela de custo"),
            new(ProductPriceTableKind.Sale, "Tabela de venda")
        ];
        _selectedProductPriceTableOption = ProductPriceTableOptions[0];
        FilteredProducts = [];
        RefreshProductFilter();
        FlexibleList = new FlexibleListViewModel(
            catalog,
            CategoryOptions,
            _businessSettings,
            SaveQuote);
        QuoteManager = new QuoteManagerViewModel(_quoteRepository);
        PricingSettings = new PricingSettingsViewModel(
            _businessSettings,
            CategoryOptions,
            SaveBusinessSettings,
            apiSettings,
            settings => SaveApiSettings(apiSettingsPath, settings),
            TestApiConnectionAsync,
            ApplicationThemeService.Apply);
        BulkDescriptionOperations =
        [
            new("Substituir descrição", BulkDescriptionMode.Replace),
            new("Adicionar no início", BulkDescriptionMode.Prepend),
            new("Adicionar no final", BulkDescriptionMode.Append)
        ];
        _selectedBulkDescriptionOperation = BulkDescriptionOperations[0];
        ImportSources =
        [
            ImportSource(
                ComponentCategory.Processor,
                "Processadores",
                "CPUs AMD e Intel",
                "CPU",
                "/hardware/processadores",
                catalog),
            ImportSource(
                ComponentCategory.Cooler,
                "Coolers",
                "Air coolers e water coolers",
                "COOL",
                "/hardware/coolers",
                catalog),
            ImportSource(
                ComponentCategory.Motherboard,
                "Placas-mãe",
                "Modelos AMD e Intel",
                "MB",
                "/hardware/placas-mae",
                catalog),
            ImportSource(
                ComponentCategory.Memory,
                "Memórias",
                "Módulos DDR4 e DDR5",
                "RAM",
                "/hardware/memoria-ram",
                catalog),
            ImportSource(
                ComponentCategory.GraphicsCard,
                "Placas de vídeo",
                "GPUs AMD, Intel e NVIDIA",
                "GPU",
                "/hardware/placa-de-video-vga",
                catalog),
            ImportSource(
                ComponentCategory.HardDrive,
                "Discos rígidos (HD)",
                "HDs internos, externos e corporativos",
                "Storage",
                "/hardware/disco-rigido-hd",
                catalog,
                sourceKey: "kabum-hd"),
            ImportSource(
                ComponentCategory.Storage,
                "SSDs",
                "SSDs SATA e NVMe",
                "Storage",
                "/hardware/ssd-2-5",
                catalog),
            ImportSource(
                ComponentCategory.PowerSupply,
                "Fontes",
                "Fontes de alimentação",
                "PSU",
                "/hardware/fontes",
                catalog),
            ImportSource(
                ComponentCategory.Case,
                "Gabinetes",
                "Gabinetes gamer e office",
                "CASE",
                "/perifericos/gabinetes",
                catalog),
            ImportSource(
                ComponentCategory.Monitor,
                "Monitores",
                "Monitores gamer, profissionais e office",
                "Monitor",
                "/computadores/monitores",
                catalog),
            ImportSource(
                ComponentCategory.Mouse,
                "Mouses",
                "Mouses com fio e sem fio",
                "Mouse",
                "/perifericos/teclado-mouse",
                catalog),
            ImportSource(
                ComponentCategory.Keyboard,
                "Teclados",
                "Teclados mecânicos, membrana e sem fio",
                "Keyboard",
                "/perifericos/teclado-mouse",
                catalog)
        ];
        SelectedProductCategory = CategoryOptions[0];
        ClearCommand = new RelayCommand(Clear);
        BalancedPresetCommand = new RelayCommand(ApplyBalancedPreset);
        PerformancePresetCommand = new RelayCommand(ApplyPerformancePreset);
        ShowFlexibleListCommand = new RelayCommand(() => ShowView("flexible-list"));
        ShowProductsCommand = new RelayCommand(() => ShowView("products"));
        ShowImportsCommand = new RelayCommand(() => ShowView("imports"));
        ShowQuotesCommand = new RelayCommand(() => ShowView("quotes"));
        ShowSettingsCommand = new RelayCommand(() => ShowView("settings"));
        SaveProductCommand = new RelayCommand(SaveProduct);
        NewProductCommand = new RelayCommand(BeginNewProduct);
        EditProductCommand = new RelayCommand(BeginEditProduct);
        RequestDeleteProductCommand = new RelayCommand(RequestDeleteProduct);
        ConfirmDeleteProductCommand = new RelayCommand(ConfirmDeleteProduct);
        CancelDeleteProductCommand = new RelayCommand(CancelDeleteProduct);
        ApplyBulkDescriptionCommand = new RelayCommand(ApplyBulkDescription);
        RequestBulkDeleteCommand = new RelayCommand(RequestBulkDelete);
        ConfirmBulkDeleteCommand = new RelayCommand(ConfirmBulkDelete);
        CancelBulkDeleteCommand = new RelayCommand(CancelBulkDelete);
        SortCatalogByDescriptionCommand = new RelayCommand(SortCatalogByDescription);
        SortCatalogByCostCommand = new RelayCommand(SortCatalogByCost);
        RemoveProductImageCommand = new RelayCommand(RemoveProductImage);
        CancelProductEditCommand = new RelayCommand(CancelProductEdit);
        ImportAllCommand = new AsyncRelayCommand(ImportAllAsync);
        RefreshSummary();
    }

    public ObservableCollection<ComponentSlotViewModel> Slots { get; }
    public ObservableCollection<SelectedLineViewModel> SelectedItems { get; }
    public ObservableCollection<CompatibilityIssueViewModel> Issues { get; }
    public ObservableCollection<ProductListItemViewModel> Products { get; }
    public ObservableCollection<ProductListItemViewModel> FilteredProducts { get; }
    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }
    public IReadOnlyList<ProductCategoryFilterViewModel> ProductCategoryFilters { get; }
    public IReadOnlyList<ProductCatalogSortOptionViewModel> CatalogSortOptions { get; }
    public IReadOnlyList<ProductPriceTableOptionViewModel> ProductPriceTableOptions { get; }
    public ObservableCollection<ImportSourceViewModel> ImportSources { get; }
    public IReadOnlyList<ProductDescriptionOperationViewModel> BulkDescriptionOperations { get; }
    public FlexibleListViewModel FlexibleList { get; }
    public QuoteManagerViewModel QuoteManager { get; }
    public PricingSettingsViewModel PricingSettings { get; }
    public ICommand ClearCommand { get; }
    public ICommand BalancedPresetCommand { get; }
    public ICommand PerformancePresetCommand { get; }
    public ICommand ShowFlexibleListCommand { get; }
    public ICommand ShowProductsCommand { get; }
    public ICommand ShowImportsCommand { get; }
    public ICommand ShowQuotesCommand { get; }
    public ICommand ShowSettingsCommand { get; }
    public ICommand SaveProductCommand { get; }
    public ICommand NewProductCommand { get; }
    public ICommand EditProductCommand { get; }
    public ICommand RequestDeleteProductCommand { get; }
    public ICommand ConfirmDeleteProductCommand { get; }
    public ICommand CancelDeleteProductCommand { get; }
    public ICommand ApplyBulkDescriptionCommand { get; }
    public ICommand RequestBulkDeleteCommand { get; }
    public ICommand ConfirmBulkDeleteCommand { get; }
    public ICommand CancelBulkDeleteCommand { get; }
    public ICommand SortCatalogByDescriptionCommand { get; }
    public ICommand SortCatalogByCostCommand { get; }
    public ICommand RemoveProductImageCommand { get; }
    public ICommand CancelProductEditCommand { get; }
    public ICommand ImportAllCommand { get; }

    public bool IsAssemblyView => false;
    public bool IsFlexibleListView => _currentView == "flexible-list";
    public bool IsProductsView => _currentView == "products";
    public bool IsImportsView => _currentView == "imports";
    public bool IsQuotesView => _currentView == "quotes";
    public bool IsSettingsView => _currentView == "settings";

    public int CatalogCount => Products.Count;
    public string CatalogCountText => $"{CatalogCount} produtos disponíveis";

    public ProductPriceTableOptionViewModel SelectedProductPriceTableOption
    {
        get => _selectedProductPriceTableOption;
        set
        {
            if (value is not null &&
                SetProperty(ref _selectedProductPriceTableOption, value))
            {
                ProductPriceTableStatusMessage = string.Empty;
                OnPropertyChanged(nameof(ProductPriceTableSuggestedFileName));
            }
        }
    }

    public bool IsExportingProductPriceTable
    {
        get => _isExportingProductPriceTable;
        private set
        {
            if (SetProperty(ref _isExportingProductPriceTable, value))
            {
                OnPropertyChanged(nameof(CanExportProductPriceTable));
                OnPropertyChanged(nameof(ProductPriceTableButtonText));
            }
        }
    }

    public bool CanExportProductPriceTable =>
        FilteredProducts.Count > 0 && !IsExportingProductPriceTable;

    public string ProductPriceTableButtonText =>
        IsExportingProductPriceTable ? "Exportando..." : "Exportar PDF";

    public string ProductPriceTableStatusMessage
    {
        get => _productPriceTableStatusMessage;
        private set => SetProperty(ref _productPriceTableStatusMessage, value);
    }

    public string ProductPriceTableSuggestedFileName
    {
        get
        {
            var table = SelectedProductPriceTableOption.Kind ==
                        ProductPriceTableKind.Cost
                ? "custos"
                : "vendas";
            var scope = SelectedCatalogCategoryFilter.Value?.ToString().ToLowerInvariant() ??
                        "todos";
            return $"tabela-{table}-{scope}.pdf";
        }
    }

    public ProductListItemViewModel? SelectedCatalogProduct => _selectedCatalogProduct;
    public bool IsProductFormVisible => SelectedCatalogProduct is null;
    public bool IsProductDetailVisible => SelectedCatalogProduct is not null;
    public bool IsEditingProduct => _editingProductId is not null;
    public string ProductFormTitle =>
        IsEditingProduct ? "Editar produto" : "Cadastrar novo produto";
    public string ProductSaveButtonText =>
        IsEditingProduct ? "Salvar alterações" : "Cadastrar produto";

    public bool IsDeleteConfirmationVisible
    {
        get => _isDeleteConfirmationVisible;
        private set => SetProperty(ref _isDeleteConfirmationVisible, value);
    }

    public string BulkDescriptionText
    {
        get => _bulkDescriptionText;
        set
        {
            if (SetProperty(ref _bulkDescriptionText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(CanApplyBulkDescription));
            }
        }
    }

    public ProductDescriptionOperationViewModel SelectedBulkDescriptionOperation
    {
        get => _selectedBulkDescriptionOperation;
        set => SetProperty(ref _selectedBulkDescriptionOperation, value);
    }

    public int BulkSelectedCount => Products.Count(product => product.IsBulkSelected);
    public bool HasBulkSelection => BulkSelectedCount > 0;
    public bool CanApplyBulkDescription =>
        HasBulkSelection && !string.IsNullOrWhiteSpace(BulkDescriptionText);
    public string BulkSelectedText =>
        BulkSelectedCount == 1
            ? "1 produto marcado"
            : $"{BulkSelectedCount} produtos marcados";

    public string BulkStatusMessage
    {
        get => _bulkStatusMessage;
        private set => SetProperty(ref _bulkStatusMessage, value);
    }

    public ProductCategoryFilterViewModel SelectedCatalogCategoryFilter
    {
        get => _selectedCatalogCategoryFilter;
        set
        {
            if (value is not null &&
                SetProperty(ref _selectedCatalogCategoryFilter, value))
            {
                RefreshProductFilter();
            }
        }
    }

    public bool IsBulkDeleteConfirmationVisible
    {
        get => _isBulkDeleteConfirmationVisible;
        private set => SetProperty(ref _isBulkDeleteConfirmationVisible, value);
    }

    public string FilteredCatalogCountText =>
        FilteredProducts.Count == Products.Count
            ? $"{Products.Count} produtos"
            : $"{FilteredProducts.Count} de {Products.Count} produtos";

    public string CatalogSearchText
    {
        get => _catalogSearchText;
        set
        {
            if (SetProperty(ref _catalogSearchText, value ?? string.Empty))
            {
                RefreshProductFilter();
            }
        }
    }

    public ProductCatalogSortOptionViewModel SelectedCatalogSort
    {
        get => _selectedCatalogSort;
        set
        {
            if (value is not null && SetProperty(ref _selectedCatalogSort, value))
            {
                OnPropertyChanged(nameof(IsCatalogDescriptionSortActive));
                OnPropertyChanged(nameof(IsCatalogCostSortActive));
                OnPropertyChanged(nameof(CatalogDescriptionSortIcon));
                OnPropertyChanged(nameof(CatalogCostSortIcon));
                RefreshProductFilter();
            }
        }
    }

    public bool IsCatalogDescriptionSortActive =>
        SelectedCatalogSort.Mode is
            ProductCatalogSortMode.DescriptionAscending or
            ProductCatalogSortMode.DescriptionDescending;
    public bool IsCatalogCostSortActive =>
        SelectedCatalogSort.Mode is
            ProductCatalogSortMode.PriceAscending or
            ProductCatalogSortMode.PriceDescending;
    public string CatalogDescriptionSortIcon =>
        SelectedCatalogSort.Mode == ProductCatalogSortMode.DescriptionDescending
            ? "SortDescending"
            : "SortAscending";
    public string CatalogCostSortIcon =>
        SelectedCatalogSort.Mode == ProductCatalogSortMode.PriceDescending
            ? "SortDescending"
            : "SortAscending";

    public bool AreAllFilteredProductsSelected
    {
        get => FilteredProducts.Count > 0 &&
               FilteredProducts.All(product => product.IsBulkSelected);
        set
        {
            foreach (var product in FilteredProducts)
            {
                product.IsBulkSelected = value;
            }

            OnPropertyChanged();
            BulkStatusMessage = value
                ? FilteredProducts.Count == 1
                    ? "1 produto visível marcado."
                    : $"{FilteredProducts.Count} produtos visíveis marcados."
                : "Produtos visíveis desmarcados.";
        }
    }

    public bool IsImportingAll
    {
        get => _isImportingAll;
        private set
        {
            if (SetProperty(ref _isImportingAll, value))
            {
                OnPropertyChanged(nameof(CanImportAll));
                OnPropertyChanged(nameof(ImportAllButtonText));
            }
        }
    }

    public bool CanImportAll => !IsImportingAll;
    public string ImportAllButtonText =>
        IsImportingAll ? "Importando todas as categorias..." : "Importar todos";

    public CategoryOptionViewModel? SelectedProductCategory
    {
        get => _selectedProductCategory;
        set => SetProperty(ref _selectedProductCategory, value);
    }

    public string ProductName
    {
        get => _productName;
        set => SetProperty(ref _productName, value);
    }

    public string ProductBrand
    {
        get => _productBrand;
        set => SetProperty(ref _productBrand, value);
    }

    public string ProductDescription
    {
        get => _productDescription;
        set => SetProperty(ref _productDescription, value);
    }

    public string ProductPrice
    {
        get => _productPrice;
        set => SetProperty(ref _productPrice, value);
    }

    public string ProductImagePath
    {
        get => _productImagePath;
        private set
        {
            if (SetProperty(ref _productImagePath, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasProductImage));
            }
        }
    }

    public bool HasProductImage => !string.IsNullOrWhiteSpace(ProductImagePath);

    public void SetProductImage(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            ProductImagePath = path.Trim();
        }
    }

    public string ProductPower
    {
        get => _productPower;
        set => SetProperty(ref _productPower, value);
    }

    public string ProductSocket
    {
        get => _productSocket;
        set => SetProperty(ref _productSocket, value);
    }

    public string ProductMemoryType
    {
        get => _productMemoryType;
        set => SetProperty(ref _productMemoryType, value);
    }

    public string ProductFormFactor
    {
        get => _productFormFactor;
        set => SetProperty(ref _productFormFactor, value);
    }

    public string SupportedSockets
    {
        get => _supportedSockets;
        set => SetProperty(ref _supportedSockets, value);
    }

    public string SupportedFormFactors
    {
        get => _supportedFormFactors;
        set => SetProperty(ref _supportedFormFactors, value);
    }

    public string ProductFormMessage
    {
        get => _productFormMessage;
        private set
        {
            if (SetProperty(ref _productFormMessage, value))
            {
                OnPropertyChanged(nameof(IsProductFormError));
            }
        }
    }

    public bool IsProductFormSuccess
    {
        get => _isProductFormSuccess;
        private set
        {
            if (SetProperty(ref _isProductFormSuccess, value))
            {
                OnPropertyChanged(nameof(IsProductFormError));
            }
        }
    }

    public bool IsProductFormError =>
        !IsProductFormSuccess && !string.IsNullOrWhiteSpace(ProductFormMessage);

    public string TotalCost => _build.TotalCost.ToString("C", BrazilianCulture);
    public string EstimatedPower => $"{_build.EstimatedPowerWatts} W";
    public string ProgressText => $"{_build.CompletedSlots} de {TotalSlots} itens";
    public double ProgressValue => (double)_build.CompletedSlots / TotalSlots * 100;
    public bool HasItems => _build.CompletedSlots > 0;
    public string CompatibilityTitle =>
        _compatibilityService.Evaluate(_build).Any(issue => issue.Severity == IssueSeverity.Error)
            ? "Atenção à compatibilidade"
            : "Compatibilidade";

    private ComponentSlotViewModel Slot(
        string slotId,
        ComponentCategory category,
        string title,
        string subtitle,
        string icon,
        IEnumerable<PcComponent> catalog) =>
        new(
            slotId,
            category,
            title,
            subtitle,
            icon,
            catalog.Where(item => item.Category == category),
            SelectionChanged);

    private void SelectionChanged(ComponentSlotViewModel slot)
    {
        _build.Select(slot.Selected, slot.Category, slot.SlotId, slot.Quantity);
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        SelectedItems.Clear();
        foreach (var slot in Slots.Where(slot => slot.Selected is not null))
        {
            SelectedItems.Add(SelectedLineViewModel.From(slot, slot.Selected!));
        }

        Issues.Clear();
        foreach (var issue in _compatibilityService.Evaluate(_build))
        {
            Issues.Add(new CompatibilityIssueViewModel(issue));
        }

        OnPropertyChanged(nameof(TotalCost));
        OnPropertyChanged(nameof(EstimatedPower));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(CompatibilityTitle));
    }

    private void Clear()
    {
        foreach (var slot in Slots)
        {
            slot.Selected = null;
        }

        _build.Clear();
        RefreshSummary();
    }

    private void ApplyBalancedPreset() =>
        ApplyPreset("cpu-7600", "mb-b650m", "ram-32", "gpu-4060", "ssd-1tb", "psu-550", "case-air", "cooler-ag400");

    private void ApplyPerformancePreset() =>
        ApplyPreset("cpu-7800x3d", "mb-b650", "ram-32", "gpu-4070s", "ssd-2tb", "psu-850", "case-north", "cooler-ak620");

    private void ApplyPreset(params string[] componentIds)
    {
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var slot in Slots)
        {
            slot.FilterText = string.Empty;
            slot.Selected = slot.Options.FirstOrDefault(item =>
                componentIds.Contains(item.Id) && !usedIds.Contains(item.Id))?.Component;

            if (slot.Selected is not null)
            {
                usedIds.Add(slot.Selected.Id);
            }
        }
    }

    private void ShowView(string view)
    {
        if (string.Equals(_currentView, view, StringComparison.Ordinal))
        {
            return;
        }

        _currentView = view;
        OnPropertyChanged(nameof(IsAssemblyView));
        OnPropertyChanged(nameof(IsFlexibleListView));
        OnPropertyChanged(nameof(IsProductsView));
        OnPropertyChanged(nameof(IsImportsView));
        OnPropertyChanged(nameof(IsQuotesView));
        OnPropertyChanged(nameof(IsSettingsView));

        if (IsQuotesView)
        {
            QuoteManager.Refresh();
        }
    }

    private SavedQuote? SaveQuote(FlexibleListViewModel list)
    {
        try
        {
            var quote = _quoteRepository.SaveQuote(
                list.SavedQuote,
                list.ClientName,
                list.ClientPhone,
                list.Notes,
                list.BuildQuoteItems(),
                _businessSettings);
            QuoteManager.Refresh();
            return quote;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (SqliteException)
        {
            return null;
        }
    }

    private void SaveBusinessSettings(BusinessSettings settings)
    {
        _quoteRepository.SaveSettings(settings);
        _businessSettings = settings;
        ApplicationThemeService.Apply(settings.ThemeMode);
        FlexibleList.ApplySettings(settings);
    }

    private static void SaveApiSettings(
        string settingsPath,
        BuildPcApiSettings? settings)
    {
        if (settings is null)
        {
            BuildPcApiSettings.Disable(settingsPath);
            return;
        }

        settings.Save(settingsPath);
    }

    private static async Task TestApiConnectionAsync(BuildPcApiSettings settings)
    {
        using var client = new BuildPcApiClient(settings);
        await client.TestConnectionAsync();
    }

    private ImportSourceViewModel ImportSource(
        ComponentCategory category,
        string title,
        string subtitle,
        string icon,
        string path,
        IEnumerable<PcComponent> catalog,
        string sourceKey = "kabum") =>
        new(
            category,
            title,
            subtitle,
            icon,
            BuildKabumUrl(path),
            sourceKey,
            catalog.Count(component => IsImported(component, category, sourceKey)),
            _catalogRepository.GetLastImport(category, sourceKey),
            ImportSourceAsync);

    private async Task ImportAllAsync()
    {
        IsImportingAll = true;
        foreach (var source in ImportSources)
        {
            source.IsBatchImporting = true;
            source.StatusMessage = "Aguardando importação desta categoria...";
        }

        try
        {
            foreach (var source in ImportSources)
            {
                await ImportSourceAsync(source);
            }
        }
        finally
        {
            foreach (var source in ImportSources)
            {
                source.IsBatchImporting = false;
            }

            IsImportingAll = false;
        }
    }

    private async Task ImportSourceAsync(ImportSourceViewModel source)
    {
        var url = source.Url.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var importUri) ||
            importUri.Scheme is not ("http" or "https"))
        {
            source.StatusMessage = "Informe um link completo e válido para importar esta categoria.";
            return;
        }

        source.IsImporting = true;
        source.StatusMessage = "Conectando à KaBuM! e lendo o catálogo...";

        try
        {
            var imported = await _kabumCatalogImporter.FetchAsync(
                url,
                source.Category);
            var result = _catalogRepository.ReplaceImported(
                source.Category,
                source.SourceKey,
                imported);
            RefreshCatalogCollections();
            RefreshImportCounts();
            source.LastImportedAt = result.ImportedAt;
            source.StatusMessage =
                $"{result.Imported} importados • {result.Removed} anteriores removidos" +
                $" • {result.Kept} mantidos.";
        }
        catch (HttpRequestException)
        {
            source.StatusMessage =
                "Não foi possível acessar a KaBuM!. Verifique sua conexão e tente novamente.";
        }
        catch (TaskCanceledException)
        {
            source.StatusMessage = "A importação demorou mais que o esperado. Tente novamente.";
        }
        catch (InvalidDataException)
        {
            source.StatusMessage =
                "A KaBuM! alterou o formato da página e os produtos não puderam ser lidos.";
        }
        catch (JsonException)
        {
            source.StatusMessage =
                "Os dados recebidos da KaBuM! não estavam no formato esperado.";
        }
        catch (IOException)
        {
            source.StatusMessage =
                "Os produtos foram lidos, mas não foi possível salvar o catálogo local.";
        }
        catch (UnauthorizedAccessException)
        {
            source.StatusMessage =
                "Os produtos foram lidos, mas o aplicativo não tem permissão para salvá-los.";
        }
        catch (SqliteException)
        {
            source.StatusMessage =
                "Não foi possível atualizar o banco de dados SQLite.";
        }
        finally
        {
            source.IsImporting = false;
        }
    }

    private void RefreshCatalogCollections()
    {
        var selectedCatalogProductId = SelectedCatalogProduct?.Id;
        var catalog = _catalogRepository.GetAll();
        FlexibleList.UpdateCatalog(catalog);
        foreach (var slot in Slots)
        {
            var selectedId = slot.Selected?.Id;
            var selectedQuantity = slot.Quantity;
            slot.Selected = null;
            slot.ReplaceOptions(catalog.Where(item => item.Category == slot.Category));

            if (selectedId is not null)
            {
                slot.Selected = catalog.FirstOrDefault(item =>
                    item.Category == slot.Category &&
                    string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase));
                slot.Quantity = selectedQuantity;
            }
        }

        SelectCatalogProduct(null);
        Products.Clear();
        foreach (var product in catalog.Select((component, index) =>
                     ProductListItemViewModel.From(
                         component,
                         ToggleKeep,
                         SelectCatalogProduct,
                         BulkSelectionChanged,
                         index % 2 == 1)))
        {
            Products.Add(product);
        }

        if (selectedCatalogProductId is not null)
        {
            SelectCatalogProduct(Products.FirstOrDefault(product =>
                string.Equals(
                    product.Id,
                    selectedCatalogProductId,
                    StringComparison.OrdinalIgnoreCase)));
        }

        OnPropertyChanged(nameof(CatalogCount));
        OnPropertyChanged(nameof(CatalogCountText));
        RefreshProductFilter();
        BulkSelectionChanged();
    }

    private void RefreshImportCounts()
    {
        var catalog = _catalogRepository.GetAll();
        foreach (var source in ImportSources)
        {
            source.ImportedCount = catalog.Count(component =>
                IsImported(component, source.Category, source.SourceKey));
        }
    }

    private static bool IsImported(
        PcComponent component,
        ComponentCategory category,
        string sourceKey) =>
        component.Category == category &&
        string.Equals(
            component.ImportSource,
            sourceKey,
            StringComparison.OrdinalIgnoreCase);

    private static string BuildKabumUrl(string path) =>
        $"https://www.kabum.com.br{path}" +
        "?page_number=1&page_size=60" +
        "&facet_filters=eyJrYWJ1bV9wcm9kdWN0IjpbInRydWUiXX0=" +
        "&sort=most_searched";

    private bool ToggleKeep(ProductListItemViewModel product)
    {
        try
        {
            return _catalogRepository.SetKeepOnImport(product.Id, !product.IsKept);
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private void SelectCatalogProduct(ProductListItemViewModel? product)
    {
        if (ReferenceEquals(_selectedCatalogProduct, product))
        {
            return;
        }

        if (_selectedCatalogProduct is not null)
        {
            _selectedCatalogProduct.IsSelected = false;
        }

        _selectedCatalogProduct = product;
        IsDeleteConfirmationVisible = false;
        if (_selectedCatalogProduct is not null)
        {
            _selectedCatalogProduct.IsSelected = true;
        }

        OnPropertyChanged(nameof(SelectedCatalogProduct));
        OnPropertyChanged(nameof(IsProductFormVisible));
        OnPropertyChanged(nameof(IsProductDetailVisible));
    }

    private void BeginNewProduct()
    {
        SetEditingProductId(null);
        ClearProductForm();
        ProductFormMessage = string.Empty;
        IsProductFormSuccess = false;
        SelectCatalogProduct(null);
    }

    private void BeginEditProduct()
    {
        var selected = SelectedCatalogProduct?.Component;
        if (selected is null)
        {
            return;
        }

        SelectCatalogProduct(null);
        SetEditingProductId(selected.Id);
        SelectedProductCategory = CategoryOptions.First(category =>
            category.Value == selected.Category);
        ProductName = selected.Name;
        ProductBrand = selected.Brand;
        ProductDescription = selected.Description;
        ProductPrice = selected.Price.ToString("N2", BrazilianCulture);
        ProductImagePath = selected.ImageUrl ?? string.Empty;
        ProductPower = selected.PowerWatts.ToString(CultureInfo.InvariantCulture);
        ProductSocket = selected.Socket ?? string.Empty;
        ProductMemoryType = selected.MemoryType ?? string.Empty;
        ProductFormFactor = selected.FormFactor ?? string.Empty;
        SupportedSockets = string.Join(", ", selected.SupportedSockets);
        SupportedFormFactors = string.Join(", ", selected.SupportedFormFactors);
        ProductFormMessage = string.Empty;
        IsProductFormSuccess = false;
    }

    private void RemoveProductImage() => ProductImagePath = string.Empty;

    private void CancelProductEdit()
    {
        var previousId = _editingProductId;
        SetEditingProductId(null);
        ClearProductForm();
        ProductFormMessage = string.Empty;
        IsProductFormSuccess = false;
        SelectCatalogProduct(
            Products.FirstOrDefault(product =>
                string.Equals(product.Id, previousId, StringComparison.OrdinalIgnoreCase)) ??
            Products.FirstOrDefault());
    }

    private void RequestDeleteProduct()
    {
        if (SelectedCatalogProduct is not null)
        {
            IsDeleteConfirmationVisible = true;
        }
    }

    private void CancelDeleteProduct() => IsDeleteConfirmationVisible = false;

    private void ConfirmDeleteProduct()
    {
        var selected = SelectedCatalogProduct;
        if (selected is null)
        {
            return;
        }

        try
        {
            if (!_catalogRepository.Delete(selected.Id))
            {
                ProductFormMessage = "O produto não foi encontrado no catálogo.";
                IsProductFormSuccess = false;
                return;
            }
        }
        catch (SqliteException)
        {
            ProductFormMessage = "Não foi possível excluir o produto do banco de dados.";
            IsProductFormSuccess = false;
            return;
        }

        SelectCatalogProduct(null);
        SetEditingProductId(null);
        ClearProductForm();
        RefreshCatalogCollections();
        IsProductFormSuccess = true;
        ProductFormMessage = "Produto excluído do catálogo.";
    }

    private void BulkSelectionChanged()
    {
        OnPropertyChanged(nameof(BulkSelectedCount));
        OnPropertyChanged(nameof(HasBulkSelection));
        OnPropertyChanged(nameof(CanApplyBulkDescription));
        OnPropertyChanged(nameof(BulkSelectedText));
        OnPropertyChanged(nameof(AreAllFilteredProductsSelected));
        if (!HasBulkSelection)
        {
            IsBulkDeleteConfirmationVisible = false;
        }
    }

    private void RefreshProductFilter()
    {
        var filtered = Products.Where(product =>
            (SelectedCatalogCategoryFilter.Value is null ||
             product.Component.Category == SelectedCatalogCategoryFilter.Value) &&
            ProductFilter.Matches(product.Component, CatalogSearchText));
        var visibleProducts = ProductCatalogSorter.Sort(
            filtered,
            SelectedCatalogSort.Mode);
        FilteredProducts.Clear();
        for (var index = 0; index < visibleProducts.Count; index++)
        {
            var product = visibleProducts[index];
            product.SetAlternate(index % 2 == 1);
            FilteredProducts.Add(product);
        }

        OnPropertyChanged(nameof(FilteredCatalogCountText));
        OnPropertyChanged(nameof(AreAllFilteredProductsSelected));
        OnPropertyChanged(nameof(CanExportProductPriceTable));
        OnPropertyChanged(nameof(ProductPriceTableSuggestedFileName));
    }

    public ProductPriceTableDocument BuildProductPriceTableDocument()
    {
        var isCost = SelectedProductPriceTableOption.Kind ==
                     ProductPriceTableKind.Cost;
        var rows = ProductPriceTableRowFactory.Create(
            FilteredProducts,
            component =>
                isCost
                    ? component.Price
                    : FlexibleListItemViewModel.CalculateSalePrice(
                        component.Price,
                        _businessSettings.MarginFor(component.Category)));
        return new ProductPriceTableDocument(
            isCost ? "Tabela de custo" : "Tabela de venda",
            isCost ? "Custo" : "Venda",
            SelectedCatalogCategoryFilter.Name,
            CatalogSearchText.Trim(),
            _businessSettings.CompanyName,
            DateTimeOffset.Now,
            rows,
            GroupByCategory: isCost);
    }

    public void BeginProductPriceTableExport()
    {
        IsExportingProductPriceTable = true;
        ProductPriceTableStatusMessage =
            "Preparando fotos e gerando a tabela...";
    }

    public void CompleteProductPriceTableExport(bool opened)
    {
        IsExportingProductPriceTable = false;
        ProductPriceTableStatusMessage = opened
            ? "PDF aberto. Use o visualizador para salvar ou imprimir."
            : "O PDF foi gerado, mas não foi possível abri-lo automaticamente.";
    }

    public void FailProductPriceTableExport()
    {
        IsExportingProductPriceTable = false;
        ProductPriceTableStatusMessage =
            "Não foi possível gerar a visualização do PDF.";
    }

    private void SortCatalogByDescription()
    {
        var mode = SelectedCatalogSort.Mode ==
                   ProductCatalogSortMode.DescriptionAscending
            ? ProductCatalogSortMode.DescriptionDescending
            : ProductCatalogSortMode.DescriptionAscending;
        SelectedCatalogSort = CatalogSortOptions.Single(option => option.Mode == mode);
    }

    private void SortCatalogByCost()
    {
        var mode = SelectedCatalogSort.Mode == ProductCatalogSortMode.PriceAscending
            ? ProductCatalogSortMode.PriceDescending
            : ProductCatalogSortMode.PriceAscending;
        SelectedCatalogSort = CatalogSortOptions.Single(option => option.Mode == mode);
    }

    private void RequestBulkDelete()
    {
        if (HasBulkSelection)
        {
            IsBulkDeleteConfirmationVisible = true;
        }
    }

    private void CancelBulkDelete() => IsBulkDeleteConfirmationVisible = false;

    private void ConfirmBulkDelete()
    {
        var selectedIds = Products
            .Where(product => product.IsBulkSelected)
            .Select(product => product.Id)
            .ToList();
        if (selectedIds.Count == 0)
        {
            IsBulkDeleteConfirmationVisible = false;
            return;
        }

        try
        {
            var deleted = _catalogRepository.DeleteMany(selectedIds);
            IsBulkDeleteConfirmationVisible = false;
            RefreshCatalogCollections();
            BulkStatusMessage = deleted == 1
                ? "1 produto apagado."
                : $"{deleted} produtos apagados.";
        }
        catch (SqliteException)
        {
            BulkStatusMessage = "Não foi possível apagar os produtos selecionados.";
        }
    }

    private void ApplyBulkDescription()
    {
        var selectedIds = Products
            .Where(product => product.IsBulkSelected)
            .Select(product => product.Id)
            .ToList();
        if (selectedIds.Count == 0 || string.IsNullOrWhiteSpace(BulkDescriptionText))
        {
            BulkStatusMessage = "Marque os produtos e informe o texto da descrição.";
            return;
        }

        try
        {
            var updated = _catalogRepository.UpdateDescriptions(
                selectedIds,
                BulkDescriptionText,
                SelectedBulkDescriptionOperation.Mode);
            RefreshCatalogCollections();
            BulkDescriptionText = string.Empty;
            BulkStatusMessage = updated == 1
                ? "Descrição de 1 produto atualizada."
                : $"Descrições de {updated} produtos atualizadas.";
        }
        catch (SqliteException)
        {
            BulkStatusMessage = "Não foi possível atualizar as descrições no banco de dados.";
        }
    }

    private void SaveProduct()
    {
        IsProductFormSuccess = false;

        if (SelectedProductCategory is null ||
            string.IsNullOrWhiteSpace(ProductName) ||
            string.IsNullOrWhiteSpace(ProductBrand) ||
            string.IsNullOrWhiteSpace(ProductDescription))
        {
            ProductFormMessage = "Preencha categoria, nome, marca e descrição.";
            return;
        }

        if (!TryParsePrice(ProductPrice, out var price) || price <= 0)
        {
            ProductFormMessage = "Informe um preço válido, por exemplo 1299,90.";
            return;
        }

        var existing = _editingProductId is null
            ? null
            : Products.FirstOrDefault(product =>
                string.Equals(product.Id, _editingProductId, StringComparison.OrdinalIgnoreCase))
                ?.Component;
        if (_editingProductId is not null && existing is null)
        {
            ProductFormMessage = "O produto que estava sendo editado não foi encontrado.";
            return;
        }

        var componentId = existing?.Id ?? $"custom-{Guid.NewGuid():N}";
        string? imageUrl;
        try
        {
            imageUrl = PersistProductImage(componentId, ProductImagePath);
        }
        catch (IOException)
        {
            ProductFormMessage = "Não foi possível copiar a foto selecionada.";
            return;
        }
        catch (UnauthorizedAccessException)
        {
            ProductFormMessage = "O aplicativo não tem permissão para salvar a foto selecionada.";
            return;
        }

        var component = new PcComponent
        {
            Id = componentId,
            Category = SelectedProductCategory.Value,
            Name = ProductName.Trim(),
            Brand = ProductBrand.Trim(),
            Description = ProductDescription.Trim(),
            Price = price,
            PowerWatts = existing?.PowerWatts ?? 0,
            Socket = existing?.Socket,
            MemoryType = existing?.MemoryType,
            FormFactor = existing?.FormFactor,
            SupportedSockets = existing?.SupportedSockets ??
                               new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            SupportedFormFactors = existing?.SupportedFormFactors ??
                                   new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ImageUrl = imageUrl,
            ImportSource = existing?.ImportSource,
            KeepOnImport = existing?.ImportSource is not null || existing?.KeepOnImport == true,
            IsUserDefined = existing?.IsUserDefined ?? true
        };

        try
        {
            if (existing is null)
            {
                _catalogRepository.Add(component);
            }
            else if (!_catalogRepository.Update(component))
            {
                ProductFormMessage = "O produto não foi encontrado para atualização.";
                return;
            }
        }
        catch (IOException)
        {
            ProductFormMessage = "Não foi possível salvar o produto. Verifique o acesso ao armazenamento local.";
            return;
        }
        catch (UnauthorizedAccessException)
        {
            ProductFormMessage = "Não foi possível salvar o produto. Verifique o acesso ao armazenamento local.";
            return;
        }
        catch (SqliteException)
        {
            ProductFormMessage = "Não foi possível salvar o produto no banco de dados.";
            return;
        }

        var savedId = component.Id;
        SetEditingProductId(null);
        RefreshCatalogCollections();
        ClearProductForm();
        SelectCatalogProduct(Products.FirstOrDefault(product =>
            string.Equals(product.Id, savedId, StringComparison.OrdinalIgnoreCase)));
        IsProductFormSuccess = true;
        ProductFormMessage = existing is null
            ? "Produto cadastrado e adicionado à montagem."
            : "Alterações do produto salvas.";
    }

    private void ClearProductForm()
    {
        ProductName = string.Empty;
        ProductBrand = string.Empty;
        ProductDescription = string.Empty;
        ProductPrice = string.Empty;
        ProductPower = string.Empty;
        ProductSocket = string.Empty;
        ProductMemoryType = string.Empty;
        ProductFormFactor = string.Empty;
        SupportedSockets = string.Empty;
        SupportedFormFactors = string.Empty;
        ProductImagePath = string.Empty;
    }

    private string? PersistProductImage(string componentId, string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return null;
        }

        if (Uri.TryCreate(selectedPath, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
        {
            return uri.AbsoluteUri;
        }

        var sourcePath = uri?.IsFile == true ? uri.LocalPath : selectedPath;
        if (!File.Exists(sourcePath))
        {
            throw new IOException("A foto selecionada não existe.");
        }

        var sourceInfo = new FileInfo(sourcePath);
        if (sourceInfo.Length > 5 * 1024 * 1024)
        {
            throw new IOException("A foto selecionada deve ter no máximo 5 MB.");
        }

        Directory.CreateDirectory(_productImagesDirectory);
        var fullSourcePath = Path.GetFullPath(sourcePath);
        var fullImagesDirectory = Path.GetFullPath(_productImagesDirectory)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (fullSourcePath.StartsWith(fullImagesDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return fullSourcePath;
        }

        var extension = Path.GetExtension(sourcePath);
        var safeId = string.Concat(componentId.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var destinationPath = Path.Combine(
            _productImagesDirectory,
            $"{safeId}-{Guid.NewGuid():N}{extension}");
        File.Copy(sourcePath, destinationPath, overwrite: false);
        return destinationPath;
    }

    private void SetEditingProductId(string? id)
    {
        if (string.Equals(_editingProductId, id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _editingProductId = id;
        OnPropertyChanged(nameof(IsEditingProduct));
        OnPropertyChanged(nameof(ProductFormTitle));
        OnPropertyChanged(nameof(ProductSaveButtonText));
    }

    private static bool TryParsePrice(string value, out decimal price) =>
        decimal.TryParse(value, NumberStyles.Currency, BrazilianCulture, out price) ||
        decimal.TryParse(value, NumberStyles.Currency, CultureInfo.InvariantCulture, out price);

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static HashSet<string> SplitValues(string value) =>
        new HashSet<string>(
            value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
}
