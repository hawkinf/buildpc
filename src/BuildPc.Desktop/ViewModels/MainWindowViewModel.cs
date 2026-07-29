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
    private readonly IComponentCatalogRepository _catalogRepository;
    private readonly KabumCatalogImporter _kabumCatalogImporter;
    private readonly IQuoteRepository _quoteRepository;
    private readonly BuildPcApplicationSettingsStore _applicationSettingsStore;
    private readonly IAssemblyTemplateRepository _templateRepository;
    private readonly Dictionary<string, string> _configuredImportSourceUrls;
    private readonly IDebouncer _catalogSearchDebounce;

    /// <summary>
    /// Datas da última importação de todas as categorias, lidas de uma só vez.
    /// Consultar cartão por cartão custava doze idas ao servidor a cada abertura
    /// do programa, todas na thread da interface.
    /// </summary>
    private readonly IReadOnlyDictionary<string, DateTimeOffset> _lastImports;
    private readonly ProductImageStore _productImages;
    private BuildPcApiSettings? _apiSettings;
    private bool _isApiKeyUnreadable;
    private BusinessSettings _businessSettings;
    private string _currentView = "flexible-list";
    private CategoryOptionViewModel? _selectedProductCategory;
    private string _productName = string.Empty;
    private string _productBrand = string.Empty;
    private string _productDescription = string.Empty;
    private string _productPrice = string.Empty;
    private string _productFormMessage = string.Empty;
    private bool _isProductFormSuccess;
    private bool _isProductEditCostVisible;
    private bool _isImportingAll;
    private bool _isImportConfirmationVisible;
    private bool _isImportProgressVisible;
    private bool _pendingImportAll;
    private ImportSourceViewModel? _pendingImportSource;
    private CancellationTokenSource? _importCancellation;
    private string _importConfirmationTitle = string.Empty;
    private string _importConfirmationMessage = string.Empty;
    private string _importProgressTitle = string.Empty;
    private string _importProgressCurrentItem = string.Empty;
    private string _importProgressDetail = string.Empty;
    private string _importProgressProductsText = string.Empty;
    private double _importProgressValue;
    private bool _isToolsMenuExpanded;
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
    private bool _isExportingProductPriceTable;
    private string _productPriceTableStatusMessage = string.Empty;
    private bool _isCatalogShowingSalePrice = true;

    public static CultureInfo BrazilianCulture { get; } = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Carrega tudo o que depende de banco ou rede e devolve o ViewModel pronto.
    /// </summary>
    /// <remarks>
    /// O construtor não pode aguardar, e antes fazia três chamadas bloqueantes
    /// (configurações, catálogo e datas de importação). No modo servidor isso
    /// travava a interface durante a abertura. Toda a leitura acontece aqui, e o
    /// construtor apenas monta as coleções com dados já em memória.
    /// </remarks>
    /// <param name="forceLocalDatabase">
    /// Ignora a API configurada e usa o SQLite local nesta sessão. Serve para
    /// abrir o programa quando o servidor não responde, sem alterar o arquivo
    /// de configuração.
    /// </param>
    public static async Task<MainWindowViewModel> CreateAsync(
        bool forceLocalDatabase = false,
        CancellationToken cancellationToken = default)
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BuildPC");
        var databasePath = Path.Combine(dataDirectory, "catalogo.db");
        var legacyJsonPath = Path.Combine(dataDirectory, "produtos.json");
        var legacyApiSettingsPath = Path.Combine(dataDirectory, "servidor.json");

        var applicationSettingsStore = new BuildPcApplicationSettingsStore(
            BuildPcApplicationSettingsStore.DefaultPath);
        var applicationConfiguration = applicationSettingsStore.Load();
        var legacyApiSettings = applicationConfiguration is null
            ? BuildPcApiSettings.Load(legacyApiSettingsPath)
            : null;
        var apiSettings = forceLocalDatabase
            ? null
            : applicationConfiguration?.ApiSettings ?? legacyApiSettings;

        IComponentCatalogRepository catalogRepository;
        IQuoteRepository quoteRepository;
        IAssemblyTemplateRepository templateRepository;
        if (apiSettings is not null)
        {
            var apiClient = new BuildPcApiClient(apiSettings);
            catalogRepository = apiClient;
            quoteRepository = apiClient;
            templateRepository = apiClient;
        }
        else
        {
            catalogRepository = new ComponentCatalogRepository(
                databasePath,
                legacyJsonPath);
            var localQuotes = new QuoteRepository(databasePath);
            quoteRepository = localQuotes;
            templateRepository = localQuotes;

            // O modo servidor tem backup diario na VPS; o modo local nao tinha
            // nenhum. A copia roda apos os repositorios criarem o schema, e uma
            // falha aqui nunca impede o programa de abrir.
            try
            {
                new LocalDatabaseBackupService(
                        databasePath,
                        Path.Combine(dataDirectory, "backups"))
                    .CreateDailyBackup(DateTimeOffset.Now);
            }
            catch (Exception exception)
                when (exception is IOException
                          or UnauthorizedAccessException
                          or SqliteException)
            {
                CrashLogService.Record("Backup do banco local", exception);
            }
        }

        var businessSettings =
            applicationConfiguration?.Application ??
            await quoteRepository.GetSettingsAsync(cancellationToken)
                .ConfigureAwait(false);
        var catalog = await catalogRepository.GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        var lastImports = await catalogRepository
            .GetLastImportsAsync(cancellationToken)
            .ConfigureAwait(false);

        return new MainWindowViewModel(new StartupContext(
            forceLocalDatabase,
            dataDirectory,
            applicationSettingsStore,
            applicationConfiguration,
            apiSettings,
            catalogRepository,
            quoteRepository,
            templateRepository,
            businessSettings,
            catalog,
            lastImports));
    }

    /// <summary>Dados já carregados, para o construtor não fazer I/O.</summary>
    private sealed record StartupContext(
        bool ForceLocalDatabase,
        string DataDirectory,
        BuildPcApplicationSettingsStore SettingsStore,
        BuildPcApplicationConfiguration? Configuration,
        BuildPcApiSettings? ApiSettings,
        IComponentCatalogRepository CatalogRepository,
        IQuoteRepository QuoteRepository,
        IAssemblyTemplateRepository TemplateRepository,
        BusinessSettings BusinessSettings,
        IReadOnlyList<PcComponent> Catalog,
        IReadOnlyDictionary<string, DateTimeOffset> LastImports);

    private MainWindowViewModel(StartupContext context)
    {
        _productImages = new ProductImageStore(
            Path.Combine(context.DataDirectory, "imagens-produtos"));
        _applicationSettingsStore = context.SettingsStore;
        _apiSettings = context.ApiSettings;
        IsUsingLocalDatabaseFallback = context.ForceLocalDatabase;
        _isApiKeyUnreadable = context.Configuration?.IsApiKeyUnreadable ?? false;
        _configuredImportSourceUrls =
            context.Configuration?.ImportSourceUrls.ToDictionary(
                entry => entry.Key,
                entry => entry.Value,
                StringComparer.OrdinalIgnoreCase) ??
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _catalogRepository = context.CatalogRepository;
        _quoteRepository = context.QuoteRepository;
        _templateRepository = context.TemplateRepository;
        ConnectionStatus = new ConnectionStatusViewModel(
            _apiSettings,
            TestApiConnectionAsync);
        _businessSettings = context.BusinessSettings;
        _kabumCatalogImporter = new KabumCatalogImporter(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45)
        });
        var applicationConfiguration = context.Configuration;
        var catalog = context.Catalog;
        _lastImports = context.LastImports;
        var categoryDefinitions = _businessSettings.EffectiveProductCategories();
        CategoryOptions = new ObservableCollection<CategoryOptionViewModel>(
            categoryDefinitions.Select(category =>
                new CategoryOptionViewModel(category.Value, category.Name)));
        Products = new ObservableCollection<ProductListItemViewModel>(
            catalog.Select((component, index) =>
                ProductListItemViewModel.From(
                    component,
                    CategoryNameFor(component.Category),
                    ToggleKeepAsync,
                    SelectCatalogProduct,
                    BulkSelectionChanged,
                    index % 2 == 1)));
        ProductCategoryFilters =
            new ObservableCollection<ProductCategoryFilterViewModel>(
            [
                new(null, "Todos"),
                .. CategoryOptions.Select(category =>
                    new ProductCategoryFilterViewModel(category.Value, category.Name))
            ]);
        _selectedCatalogCategoryFilter = ProductCategoryFilters[0];
        CatalogSortOptions =
        [
            new("Descrição: A–Z", ProductCatalogSortMode.DescriptionAscending),
            new("Descrição: Z–A", ProductCatalogSortMode.DescriptionDescending),
            new("Custo: menor primeiro", ProductCatalogSortMode.PriceAscending),
            new("Custo: maior primeiro", ProductCatalogSortMode.PriceDescending)
        ];
        _selectedCatalogSort = CatalogSortOptions[0];
        _catalogSearchDebounce = new DebounceTimer(RefreshProductFilter);
        FilteredProducts = [];
        RefreshCatalogDisplayPrices();
        RefreshProductFilter();
        FlexibleList = new FlexibleListViewModel(
            catalog,
            CategoryOptions,
            _businessSettings,
            SaveQuoteAsync);
        PriceLookup = new PriceLookupViewModel(
            catalog,
            categoryDefinitions,
            _businessSettings);
        QuoteManager = new QuoteManagerViewModel(
            _quoteRepository,
            OpenQuoteInAssembly,
            DuplicateQuoteInAssembly);
        PricingSettings = new PricingSettingsViewModel(
            _businessSettings,
            CategoryOptions,
            SaveBusinessSettingsAsync,
            _apiSettings,
            SaveApiSettings,
            TestApiConnectionAsync,
            ApplicationThemeService.Apply,
            _isApiKeyUnreadable);
        CategoryManagement = new CategoryManagementViewModel(
            categoryDefinitions,
            CategoryProductCount,
            SaveProductCategoriesAsync);
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
        ShowFlexibleListCommand = new RelayCommand(() => ShowView("flexible-list"));
        ShowProductsCommand = new RelayCommand(() => ShowToolView("products"));
        ShowPriceLookupCommand = new RelayCommand(() => ShowView("price-lookup"));
        ShowProductManagementCommand = new RelayCommand(ShowProductManagement);
        ShowCategoryManagementCommand =
            new RelayCommand(() => ShowToolView("category-management"));
        ShowImportsCommand = new RelayCommand(() => ShowToolView("imports"));
        ShowQuotesCommand = new RelayCommand(() => ShowView("quotes"));
        ShowSettingsCommand = new RelayCommand(() => ShowToolView("settings"));
        ToggleToolsMenuCommand = new RelayCommand(ToggleToolsMenu);
        SaveProductCommand = new AsyncRelayCommand(SaveProductAsync);
        NewProductCommand = new RelayCommand(BeginNewProduct);
        EditProductCommand = new AsyncRelayCommand(BeginEditProductAsync);
        RequestDeleteProductCommand = new RelayCommand(RequestDeleteProduct);
        ConfirmDeleteProductCommand = new AsyncRelayCommand(ConfirmDeleteProductAsync);
        CancelDeleteProductCommand = new RelayCommand(CancelDeleteProduct);
        ApplyBulkDescriptionCommand = new AsyncRelayCommand(ApplyBulkDescriptionAsync);
        RequestBulkDeleteCommand = new RelayCommand(RequestBulkDelete);
        ConfirmBulkDeleteCommand = new AsyncRelayCommand(ConfirmBulkDeleteAsync);
        CancelBulkDeleteCommand = new RelayCommand(CancelBulkDelete);
        SortCatalogByDescriptionCommand = new RelayCommand(SortCatalogByDescription);
        SortCatalogByCostCommand = new RelayCommand(SortCatalogByCost);
        ShowCatalogCostCommand = new RelayCommand(() => SetCatalogPriceMode(false));
        ShowCatalogSaleCommand = new RelayCommand(() => SetCatalogPriceMode(true));
        RemoveProductImageCommand = new RelayCommand(RemoveProductImage);
        CancelProductEditCommand = new RelayCommand(CancelProductEdit);
        ImportAllCommand = new RelayCommand(RequestImportAll);
        ConfirmImportCommand = new AsyncRelayCommand(ConfirmImportAsync);
        CancelImportConfirmationCommand = new RelayCommand(CancelImportConfirmation);
        CancelCurrentImportCommand = new RelayCommand(CancelCurrentImport);
        if (applicationConfiguration is null)
        {
            // Primeiro início ou migração do formato legado: só aqui o arquivo
            // distribuível precisa ser criado. Gravar em todo início apagaria
            // configurações que este computador não consegue ler.
            SaveApplicationConfiguration();
        }

        // O servidor.json legado ja foi lido em CreateAsync; remove-lo
        // aqui evita que a migracao volte a acontecer no proximo inicio.
        BuildPcApiSettings.Disable(
            Path.Combine(context.DataDirectory, "servidor.json"));
    }

    public ObservableCollection<ProductListItemViewModel> Products { get; }

    /// <summary>Mudanças de custo do produto em edição.</summary>
    public ObservableCollection<PriceHistoryItemViewModel> PriceHistory { get; } = [];

    public bool HasPriceHistory => PriceHistory.Count > 0;

    public string PriceHistoryCountText =>
        PriceHistory.Count == 1
            ? "1 mudança de custo registrada"
            : $"{PriceHistory.Count} mudanças de custo registradas";
    public ObservableCollection<ProductListItemViewModel> FilteredProducts { get; }
    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }
    public ObservableCollection<ProductCategoryFilterViewModel> ProductCategoryFilters { get; }
    public IReadOnlyList<ProductCatalogSortOptionViewModel> CatalogSortOptions { get; }
    public ObservableCollection<ImportSourceViewModel> ImportSources { get; }
    public IReadOnlyList<ProductDescriptionOperationViewModel> BulkDescriptionOperations { get; }
    public FlexibleListViewModel FlexibleList { get; }
    public PriceLookupViewModel PriceLookup { get; }
    public QuoteManagerViewModel QuoteManager { get; }
    public PricingSettingsViewModel PricingSettings { get; }
    public CategoryManagementViewModel CategoryManagement { get; }
    public ConnectionStatusViewModel ConnectionStatus { get; }
    public ICommand ShowFlexibleListCommand { get; }
    public ICommand ShowProductsCommand { get; }
    public ICommand ShowPriceLookupCommand { get; }
    public ICommand ShowProductManagementCommand { get; }
    public ICommand ShowCategoryManagementCommand { get; }
    public ICommand ShowImportsCommand { get; }
    public ICommand ShowQuotesCommand { get; }
    public ICommand ShowSettingsCommand { get; }
    public ICommand ToggleToolsMenuCommand { get; }
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
    public ICommand ShowCatalogCostCommand { get; }
    public ICommand ShowCatalogSaleCommand { get; }
    public ICommand RemoveProductImageCommand { get; }
    public ICommand CancelProductEditCommand { get; }
    public ICommand ImportAllCommand { get; }
    public ICommand ConfirmImportCommand { get; }
    public ICommand CancelImportConfirmationCommand { get; }
    public ICommand CancelCurrentImportCommand { get; }

    /// <summary>
    /// Verdadeiro quando o usuário optou por abrir com o banco local porque o
    /// servidor configurado não respondeu. A configuração permanece intacta.
    /// </summary>
    public bool IsUsingLocalDatabaseFallback { get; }

    public bool IsFlexibleListView => _currentView == "flexible-list";
    public bool IsProductsView => _currentView == "products";
    public bool IsPriceLookupView => _currentView == "price-lookup";
    public bool IsProductManagementView => _currentView == "product-management";
    public bool IsCategoryManagementView => _currentView == "category-management";
    public bool IsImportsView => _currentView == "imports";
    public bool IsQuotesView => _currentView == "quotes";
    public bool IsSettingsView => _currentView == "settings";
    public bool IsProductCatalogManagementActive =>
        IsProductsView || IsProductManagementView;
    public bool IsToolsView =>
        IsProductsView ||
        IsImportsView ||
        IsProductManagementView ||
        IsCategoryManagementView ||
        IsSettingsView;

    public bool IsToolsMenuExpanded
    {
        get => _isToolsMenuExpanded;
        private set
        {
            if (SetProperty(ref _isToolsMenuExpanded, value))
            {
                OnPropertyChanged(nameof(ToolsMenuIndicator));
            }
        }
    }

    public string ToolsMenuIndicator => IsToolsMenuExpanded ? "▾" : "›";

    public int CatalogCount => Products.Count;
    public string CatalogCountText => $"{CatalogCount} produtos disponíveis";
    public IEnumerable<ProductCategoryFilterViewModel> CatalogCategorySummaries =>
        ProductCategoryFilters.Where(category => category.Value is not null);
    public bool HasSelectedCatalogProduct => SelectedCatalogProduct is not null;

    public bool IsExportingProductPriceTable
    {
        get => _isExportingProductPriceTable;
        private set
        {
            if (SetProperty(ref _isExportingProductPriceTable, value))
            {
                OnPropertyChanged(nameof(CanExportProductPriceTable));
            }
        }
    }

    public bool CanExportProductPriceTable =>
        FilteredProducts.Count > 0 && !IsExportingProductPriceTable;

    public string ProductPriceTableStatusMessage
    {
        get => _productPriceTableStatusMessage;
        private set => SetProperty(ref _productPriceTableStatusMessage, value);
    }

    public string ProductPriceTableSuggestedFileName
    {
        get
        {
            var table = _isCatalogShowingSalePrice ? "vendas" : "custos";
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
                // Filtrar a cada tecla percorre todo o catálogo; esperar o fim
                // da digitação mantém a tela responsiva com milhares de itens.
                _catalogSearchDebounce.Trigger();
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
    public bool IsCatalogCostDisplayActive => !_isCatalogShowingSalePrice;
    public bool IsCatalogSaleDisplayActive => _isCatalogShowingSalePrice;
    public string CatalogPriceColumnTitle =>
        _isCatalogShowingSalePrice ? "VENDA" : "CUSTO";
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

    public bool IsImportConfirmationVisible
    {
        get => _isImportConfirmationVisible;
        private set
        {
            if (SetProperty(ref _isImportConfirmationVisible, value))
            {
                OnPropertyChanged(nameof(CanImportAll));
            }
        }
    }

    public bool IsImportProgressVisible
    {
        get => _isImportProgressVisible;
        private set
        {
            if (SetProperty(ref _isImportProgressVisible, value))
            {
                OnPropertyChanged(nameof(CanImportAll));
                OnPropertyChanged(nameof(CanCancelCurrentImport));
            }
        }
    }

    public string ImportConfirmationTitle
    {
        get => _importConfirmationTitle;
        private set => SetProperty(ref _importConfirmationTitle, value);
    }

    public string ImportConfirmationMessage
    {
        get => _importConfirmationMessage;
        private set => SetProperty(ref _importConfirmationMessage, value);
    }

    public string ImportProgressTitle
    {
        get => _importProgressTitle;
        private set => SetProperty(ref _importProgressTitle, value);
    }

    public string ImportProgressCurrentItem
    {
        get => _importProgressCurrentItem;
        private set => SetProperty(ref _importProgressCurrentItem, value);
    }

    public string ImportProgressDetail
    {
        get => _importProgressDetail;
        private set => SetProperty(ref _importProgressDetail, value);
    }

    public string ImportProgressProductsText
    {
        get => _importProgressProductsText;
        private set => SetProperty(ref _importProgressProductsText, value);
    }

    public double ImportProgressValue
    {
        get => _importProgressValue;
        private set
        {
            if (SetProperty(
                    ref _importProgressValue,
                    Math.Clamp(value, 0d, 100d)))
            {
                OnPropertyChanged(nameof(ImportProgressPercentText));
            }
        }
    }

    public string ImportProgressPercentText =>
        $"{Math.Round(ImportProgressValue):0}%";

    public bool CanCancelCurrentImport =>
        IsImportProgressVisible &&
        _importCancellation is { IsCancellationRequested: false };

    public bool CanImportAll =>
        !IsImportingAll &&
        !IsImportConfirmationVisible &&
        !IsImportProgressVisible;

    public string ImportAllButtonText =>
        IsImportingAll ? "Importando todas as categorias..." : "Importar todos";

    public CategoryOptionViewModel? SelectedProductCategory
    {
        get => _selectedProductCategory;
        set
        {
            if (SetProperty(ref _selectedProductCategory, value))
            {
                NotifyProductPricingChanged();
            }
        }
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
        set
        {
            if (SetProperty(ref _productPrice, value))
            {
                NotifyProductPricingChanged();
            }
        }
    }

    public string ProductSalePrice
    {
        get
        {
            if (SelectedProductCategory is null ||
                !TryParsePrice(ProductPrice, out var cost) ||
                cost <= 0)
            {
                return "R$ —";
            }

            return FlexibleListItemViewModel.CalculateSalePrice(
                    cost,
                    _businessSettings.MarginFor(SelectedProductCategory.Value))
                .ToString("C", BrazilianCulture);
        }
    }

    public string ProductProfit
    {
        get
        {
            if (!TryGetProductPricing(out var cost, out var salePrice))
            {
                return "R$ —";
            }

            return (salePrice - cost).ToString("C", BrazilianCulture);
        }
    }

    public string ProductProfitPercent
    {
        get
        {
            if (!TryGetProductPricing(out var cost, out var salePrice))
            {
                return "—";
            }

            var profitPercent = decimal.Round(
                (salePrice / cost - 1m) * 100m,
                2,
                MidpointRounding.AwayFromZero);
            return $"{profitPercent.ToString("N2", BrazilianCulture)}%";
        }
    }

    public bool IsProductEditCostVisible
    {
        get => _isProductEditCostVisible;
        private set
        {
            if (SetProperty(ref _isProductEditCostVisible, value))
            {
                OnPropertyChanged(nameof(IsProductEditCostHidden));
            }
        }
    }

    public bool IsProductEditCostHidden => !IsProductEditCostVisible;

    public void SetProductEditCostVisible(bool visible) =>
        IsProductEditCostVisible = visible;

    private bool TryGetProductPricing(
        out decimal cost,
        out decimal salePrice)
    {
        if (SelectedProductCategory is null ||
            !TryParsePrice(ProductPrice, out cost) ||
            cost <= 0)
        {
            cost = 0;
            salePrice = 0;
            return false;
        }

        salePrice = FlexibleListItemViewModel.CalculateSalePrice(
            cost,
            _businessSettings.MarginFor(SelectedProductCategory.Value));
        return true;
    }

    private void NotifyProductPricingChanged()
    {
        OnPropertyChanged(nameof(ProductSalePrice));
        OnPropertyChanged(nameof(ProductProfit));
        OnPropertyChanged(nameof(ProductProfitPercent));
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

    private void ShowView(string view)
    {
        if (string.Equals(_currentView, view, StringComparison.Ordinal))
        {
            return;
        }

        _currentView = view;
        OnPropertyChanged(nameof(IsFlexibleListView));
        OnPropertyChanged(nameof(IsProductsView));
        OnPropertyChanged(nameof(IsPriceLookupView));
        OnPropertyChanged(nameof(IsProductManagementView));
        OnPropertyChanged(nameof(IsCategoryManagementView));
        OnPropertyChanged(nameof(IsImportsView));
        OnPropertyChanged(nameof(IsQuotesView));
        OnPropertyChanged(nameof(IsSettingsView));
        OnPropertyChanged(nameof(IsProductCatalogManagementActive));
        OnPropertyChanged(nameof(IsToolsView));

        if (IsQuotesView)
        {
            _ = QuoteManager.RefreshAsync();
        }
    }

    private void OpenQuoteInAssembly(SavedQuote quote)
    {
        FlexibleList.LoadQuote(quote);
        ShowView("flexible-list");
    }

    /// <summary>
    /// Abre uma cópia do orçamento na Montagem. Gravar cria um número novo e o
    /// original permanece na lista.
    /// </summary>
    private void DuplicateQuoteInAssembly(SavedQuote quote)
    {
        FlexibleList.LoadQuoteAsCopy(quote);
        ShowView("flexible-list");
    }

    private async Task<SavedQuote?> SaveQuoteAsync(FlexibleListViewModel list)
    {
        try
        {
            var quote = await _quoteRepository.SaveQuoteAsync(
                list.SavedQuote,
                list.BuildQuoteDraft(_businessSettings));
            await QuoteManager.RefreshAsync();
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
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<string?> SaveBusinessSettingsAsync(BusinessSettings settings)
    {
        try
        {
            await _quoteRepository.SaveSettingsAsync(settings);
        }
        catch (SqliteException)
        {
            return "Não foi possível salvar as configurações no banco de dados.";
        }
        catch (InvalidOperationException)
        {
            return "Não foi possível salvar as configurações no servidor.";
        }

        _businessSettings = settings;
        try
        {
            SaveApplicationConfiguration();
        }
        catch (IOException)
        {
            return "As configurações foram aplicadas, mas o arquivo " +
                   "buildpc.config.json não pôde ser gravado.";
        }
        catch (UnauthorizedAccessException)
        {
            return "As configurações foram aplicadas, mas o aplicativo não tem " +
                   "permissão para gravar buildpc.config.json.";
        }
        finally
        {
            ApplicationThemeService.Apply(settings.ThemeMode);
            FlexibleList.ApplySettings(settings);
            PriceLookup.ApplySettings(settings);
            NotifyProductPricingChanged();
            RefreshCatalogDisplayPrices();
            RefreshProductFilter();
        }

        return null;
    }

    private async Task<string?> SaveProductCategoriesAsync(
        IReadOnlyList<ProductCategoryDefinition> categories)
    {
        try
        {
            var activeCategories = categories
                .Select(category => category.Value)
                .ToHashSet();
            _businessSettings = _businessSettings with
            {
                ProductCategories = categories.ToList(),
                CategoryMargins = _businessSettings.CategoryMargins
                    .Where(margin => activeCategories.Contains(margin.Key))
                    .ToDictionary()
            };
            await _quoteRepository.SaveSettingsAsync(_businessSettings);
            SaveApplicationConfiguration();
            RebuildCategoryOptions(categories);
            FlexibleList.UpdateCategories(CategoryOptions);
            FlexibleList.ApplySettings(_businessSettings);
            PriceLookup.UpdateCategories(categories);
            PriceLookup.ApplySettings(_businessSettings);
            PricingSettings.RefreshCategories(categories);
            await RefreshCatalogCollectionsAsync(refreshCategoryManagement: false);
            return null;
        }
        catch (IOException)
        {
            return "Não foi possível salvar as categorias no armazenamento.";
        }
        catch (UnauthorizedAccessException)
        {
            return "O aplicativo não tem permissão para salvar as categorias.";
        }
        catch (SqliteException)
        {
            return "Não foi possível salvar as categorias no banco de dados.";
        }
        catch (InvalidOperationException)
        {
            return "Não foi possível salvar as categorias no servidor.";
        }
    }

    private void RebuildCategoryOptions(
        IReadOnlyList<ProductCategoryDefinition> categories)
    {
        var selectedProductCategory = SelectedProductCategory?.Value;
        var selectedFilter = SelectedCatalogCategoryFilter.Value;

        CategoryOptions.Clear();
        foreach (var category in categories
                     .OrderBy(category => category.DisplayOrder)
                     .ThenBy(
                         category => category.Name,
                         StringComparer.CurrentCultureIgnoreCase))
        {
            CategoryOptions.Add(new CategoryOptionViewModel(
                category.Value,
                category.Name));
        }

        ProductCategoryFilters.Clear();
        ProductCategoryFilters.Add(new ProductCategoryFilterViewModel(
            null,
            "Todos"));
        foreach (var category in CategoryOptions)
        {
            ProductCategoryFilters.Add(new ProductCategoryFilterViewModel(
                category.Value,
                category.Name));
        }
        OnPropertyChanged(nameof(CatalogCategorySummaries));

        SelectedProductCategory =
            CategoryOptions.FirstOrDefault(category =>
                category.Value == selectedProductCategory) ??
            CategoryOptions.First();
        SelectedCatalogCategoryFilter =
            ProductCategoryFilters.FirstOrDefault(filter =>
                filter.Value == selectedFilter) ??
            ProductCategoryFilters[0];
    }

    private int CategoryProductCount(ComponentCategory category) =>
        Products.Count(product => product.Component.Category == category);

    private string CategoryNameFor(ComponentCategory category) =>
        CategoryOptions.FirstOrDefault(option => option.Value == category)?.Name ??
        category.ToString();

    private void SaveApiSettings(BuildPcApiSettings? settings)
    {
        _apiSettings = settings;
        _isApiKeyUnreadable = false;
        SaveApplicationConfiguration();
    }

    private void SaveApplicationConfiguration()
    {
        var importSourceUrls = ImportSources.ToDictionary(
            source => ImportSourceConfigurationKey(
                source.Category,
                source.SourceKey),
            source => source.Url,
            StringComparer.OrdinalIgnoreCase);
        _applicationSettingsStore.Save(new BuildPcApplicationConfiguration
        {
            Application = _businessSettings,
            ApiSettings = _apiSettings,
            ImportSourceUrls = importSourceUrls,
            IsApiKeyUnreadable = _isApiKeyUnreadable,
            IsServerBypassed = IsUsingLocalDatabaseFallback
        });
    }

    private void ImportSourceConfigurationChanged(ImportSourceViewModel source)
    {
        _configuredImportSourceUrls[
            ImportSourceConfigurationKey(source.Category, source.SourceKey)] =
            source.Url;
        SaveApplicationConfiguration();
    }

    private static Task TestApiConnectionAsync(BuildPcApiSettings settings) =>
        BuildPcApiConnectionTester.TestAsync(settings);

    private ImportSourceViewModel ImportSource(
        ComponentCategory category,
        string title,
        string subtitle,
        string icon,
        string path,
        IEnumerable<PcComponent> catalog,
        string sourceKey = "kabum") =>
        CreateImportSource(
            category,
            title,
            subtitle,
            icon,
            path,
            catalog,
            sourceKey);

    private ImportSourceViewModel CreateImportSource(
        ComponentCategory category,
        string title,
        string subtitle,
        string icon,
        string path,
        IEnumerable<PcComponent> catalog,
        string sourceKey)
    {
        var configurationKey = ImportSourceConfigurationKey(category, sourceKey);
        var url = _configuredImportSourceUrls.TryGetValue(
            configurationKey,
            out var configuredUrl)
            ? configuredUrl
            : BuildKabumUrl(path);
        return new ImportSourceViewModel(
            category,
            title,
            subtitle,
            icon,
            url,
            sourceKey,
            catalog.Count(component => IsImported(component, category, sourceKey)),
            _lastImports.GetValueOrDefault(ImportKeys.For(category, sourceKey)),
            RequestImportSourceAsync,
            ImportSourceConfigurationChanged);
    }

    private static string ImportSourceConfigurationKey(
        ComponentCategory category,
        string sourceKey) =>
        $"{sourceKey}:{category}";

    private void RequestImportAll()
    {
        if (IsImportProgressVisible)
        {
            return;
        }

        _pendingImportAll = true;
        _pendingImportSource = null;
        ImportConfirmationTitle = "Confirmar importação completa";
        ImportConfirmationMessage =
            "TODOS os produtos importados atuais serão apagados e substituídos " +
            "pelos novos dados. Produtos manuais e itens marcados como “Manter” " +
            "serão preservados. Confirma?";
        IsImportConfirmationVisible = true;
    }

    private Task RequestImportSourceAsync(ImportSourceViewModel source)
    {
        if (IsImportProgressVisible)
        {
            return Task.CompletedTask;
        }

        _pendingImportAll = false;
        _pendingImportSource = source;
        ImportConfirmationTitle = $"Confirmar importação de {source.Title}";
        ImportConfirmationMessage =
            $"TODOS os produtos importados atuais da categoria “{source.Title}” " +
            "serão apagados e substituídos pelos novos dados. Produtos manuais e " +
            "itens marcados como “Manter” serão preservados. Confirma?";
        IsImportConfirmationVisible = true;
        return Task.CompletedTask;
    }

    private void CancelImportConfirmation()
    {
        IsImportConfirmationVisible = false;
        _pendingImportAll = false;
        _pendingImportSource = null;
    }

    private async Task ConfirmImportAsync()
    {
        var importAll = _pendingImportAll;
        var source = _pendingImportSource;
        IsImportConfirmationVisible = false;
        _pendingImportAll = false;
        _pendingImportSource = null;

        if (!importAll && source is null)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _importCancellation = cancellation;
        ImportProgressValue = 0d;
        IsImportProgressVisible = true;
        OnPropertyChanged(nameof(CanCancelCurrentImport));

        try
        {
            if (importAll)
            {
                await ImportAllAsync(cancellation.Token);
            }
            else
            {
                await ImportSourceAsync(source!, cancellation.Token, 1, 1);
            }
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            foreach (var pendingSource in ImportSources.Where(item =>
                         item.StatusMessage.StartsWith(
                             "Aguardando",
                             StringComparison.OrdinalIgnoreCase)))
            {
                pendingSource.StatusMessage =
                    "Não importado porque a operação foi cancelada.";
            }
        }
        finally
        {
            _importCancellation = null;
            OnPropertyChanged(nameof(CanCancelCurrentImport));
            IsImportProgressVisible = false;
        }
    }

    private void CancelCurrentImport()
    {
        if (_importCancellation is not
            {
                IsCancellationRequested: false
            } cancellation)
        {
            return;
        }

        ImportProgressDetail =
            "Cancelamento solicitado. Interrompendo a importação atual...";
        cancellation.Cancel();
        OnPropertyChanged(nameof(CanCancelCurrentImport));
    }

    private async Task ImportAllAsync(CancellationToken cancellationToken)
    {
        IsImportingAll = true;
        ImportProgressTitle = "Importando todas as categorias";
        foreach (var source in ImportSources)
        {
            source.IsBatchImporting = true;
            source.StatusMessage = "Aguardando importação desta categoria...";
        }

        try
        {
            for (var index = 0; index < ImportSources.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ImportSourceAsync(
                    ImportSources[index],
                    cancellationToken,
                    index + 1,
                    ImportSources.Count);
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

    private async Task ImportSourceAsync(
        ImportSourceViewModel source,
        CancellationToken cancellationToken,
        int position,
        int total)
    {
        ImportProgressTitle = total > 1
            ? "Importando todas as categorias"
            : $"Importando {source.Title}";
        ImportProgressCurrentItem = total > 1
            ? $"Categoria {position} de {total} • {source.Title}"
            : source.Title;
        ImportProgressDetail = "Validando o link da categoria...";
        ImportProgressProductsText = "Nenhum produto lido ainda.";
        SetImportProgress(position, total, 0.03d);

        var url = source.Url.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var importUri) ||
            importUri.Scheme is not ("http" or "https"))
        {
            source.StatusMessage = "Informe um link completo e válido para importar esta categoria.";
            ImportProgressDetail = source.StatusMessage;
            return;
        }

        source.IsImporting = true;
        source.StatusMessage = "Conectando à loja e lendo o catálogo...";

        try
        {
            var progress = new Progress<KabumImportProgress>(current =>
            {
                ImportProgressDetail = current.Status;
                ImportProgressProductsText = current.ProductCount == 1
                    ? "1 produto encontrado."
                    : $"{current.ProductCount} produtos encontrados.";
                var pageFraction = Math.Min(
                    0.85d,
                    0.08d + current.PageNumber * 0.06d +
                    (current.Status.StartsWith(
                        "Página",
                        StringComparison.OrdinalIgnoreCase)
                        ? 0.03d
                        : 0d));
                SetImportProgress(position, total, pageFraction);
            });
            var imported = await _kabumCatalogImporter.FetchAsync(
                url,
                source.Category,
                cancellationToken,
                progress);
            cancellationToken.ThrowIfCancellationRequested();
            SetImportProgress(position, total, 0.90d);
            ImportProgressDetail =
                $"Salvando {imported.Count} produtos de {source.Title}...";
            ImportProgressProductsText =
                $"{imported.Count} produtos prontos para salvar.";

            // Gravar milhares de produtos é lento o bastante para congelar o
            // modal de progresso se rodar na thread da interface.
            var result = await _catalogRepository.ReplaceImportedAsync(
                source.Category,
                source.SourceKey,
                imported,
                CancellationToken.None);
            await RefreshCatalogCollectionsAsync();
            source.LastImportedAt = result.ImportedAt;
            ReportImportChanges(source, result);
            source.StatusMessage =
                $"{result.Imported} importados • {result.Removed} anteriores removidos" +
                $" • {result.Kept} mantidos.";
            ImportProgressDetail = $"{source.Title} concluído.";
            ImportProgressProductsText =
                $"{result.Imported} produtos importados.";
            SetImportProgress(position, total, 1d);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            source.StatusMessage = "Importação cancelada.";
            throw;
        }
        catch (HttpRequestException)
        {
            source.StatusMessage =
                "Não foi possível acessar a loja. Verifique sua conexão e tente novamente.";
        }
        catch (TaskCanceledException)
        {
            source.StatusMessage = "A importação demorou mais que o esperado. Tente novamente.";
        }
        catch (InvalidDataException)
        {
            source.StatusMessage =
                "A loja alterou o formato da página e os produtos não puderam ser lidos.";
        }
        catch (JsonException)
        {
            source.StatusMessage =
                "Os dados recebidos da loja não estavam no formato esperado.";
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
        catch (InvalidOperationException)
        {
            source.StatusMessage =
                "Os produtos foram lidos, mas o servidor recusou a gravação. " +
                "Verifique a conexão e tente novamente.";
        }
        finally
        {
            source.IsImporting = false;
        }
    }

    /// <summary>
    /// Acrescenta ao aviso da categoria o que mudou em relação à carga
    /// anterior: variações de preço e itens que deixaram de vir.
    /// </summary>
    private static void ReportImportChanges(
        ImportSourceViewModel source,
        ImportReplaceResult result)
    {
        if (!result.HasPriceChanges && result.Disappeared == 0)
        {
            return;
        }

        var parts = new List<string>();
        if (result.Increases > 0)
        {
            parts.Add($"{result.Increases} subiram de preço");
        }

        if (result.Decreases > 0)
        {
            parts.Add($"{result.Decreases} baixaram de preço");
        }

        if (result.Disappeared > 0)
        {
            parts.Add(result.Disappeared == 1
                ? "1 saiu do catálogo da loja"
                : $"{result.Disappeared} saíram do catálogo da loja");
        }

        source.StatusMessage += $" • {string.Join(", ", parts)}.";
    }

    private void SetImportProgress(
        int position,
        int total,
        double currentCategoryProgress) =>
        ImportProgressValue = ImportProgressCalculator.Calculate(
            position,
            total,
            currentCategoryProgress);

    /// <summary>
    /// Lê o catálogo tratando a indisponibilidade do banco ou do servidor.
    /// Devolve <c>null</c> quando a leitura falha, mantendo as coleções atuais.
    /// </summary>
    private async Task<IReadOnlyList<PcComponent>?> TryReadCatalogAsync()
    {
        try
        {
            return await _catalogRepository.GetAllAsync();
        }
        catch (SqliteException)
        {
            BulkStatusMessage =
                "Não foi possível ler o catálogo no banco de dados local.";
            return null;
        }
        catch (InvalidOperationException)
        {
            BulkStatusMessage =
                "Não foi possível ler o catálogo no servidor. " +
                "A lista exibida pode estar desatualizada.";
            return null;
        }
    }

    private async Task RefreshCatalogCollectionsAsync(
        bool refreshCategoryManagement = true)
    {
        if (await TryReadCatalogAsync() is not { } catalog)
        {
            return;
        }

        var selectedCatalogProductId = SelectedCatalogProduct?.Id;
        FlexibleList.UpdateCatalog(catalog);
        PriceLookup.UpdateCatalog(catalog);
        SelectCatalogProduct(null);
        Products.Clear();
        foreach (var product in catalog.Select((component, index) =>
                     ProductListItemViewModel.From(
                         component,
                         CategoryNameFor(component.Category),
                         ToggleKeepAsync,
                         SelectCatalogProduct,
                         BulkSelectionChanged,
                         index % 2 == 1)))
        {
            Products.Add(product);
        }
        RefreshCatalogDisplayPrices();

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
        if (refreshCategoryManagement)
        {
            CategoryManagement.UpdateProductCounts();
        }
        RefreshProductFilter();
        BulkSelectionChanged();
        RefreshImportCounts(catalog);
    }

    /// <summary>
    /// Recebe o catálogo já lido para evitar uma segunda varredura completa —
    /// no modo servidor cada leitura é uma chamada HTTP.
    /// </summary>
    private void RefreshImportCounts(IReadOnlyList<PcComponent> catalog)
    {
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

    private async Task<bool> ToggleKeepAsync(ProductListItemViewModel product)
    {
        try
        {
            return await _catalogRepository.SetKeepOnImportAsync(
                product.Id,
                !product.IsKept);
        }
        catch (SqliteException)
        {
            return false;
        }
        catch (InvalidOperationException)
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
        OnPropertyChanged(nameof(HasSelectedCatalogProduct));
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

    private void ShowProductManagement()
    {
        BeginNewProduct();
        ShowToolView("product-management");
    }

    private void ShowToolView(string view)
    {
        IsToolsMenuExpanded = true;
        ShowView(view);
    }

    private void ToggleToolsMenu() =>
        IsToolsMenuExpanded = !IsToolsMenuExpanded;

    private async Task BeginEditProductAsync()
    {
        var selected = SelectedCatalogProduct?.Component;
        if (selected is null)
        {
            return;
        }

        SelectCatalogProduct(null);
        SetProductEditCostVisible(false);
        SetEditingProductId(selected.Id);
        SelectedProductCategory = CategoryOptions.First(category =>
            category.Value == selected.Category);
        ProductName = selected.Name;
        ProductBrand = selected.Brand;
        ProductDescription = selected.Description;
        ProductPrice = selected.Price.ToString("N2", BrazilianCulture);
        ProductImagePath = selected.ImageUrl ?? string.Empty;
        ProductFormMessage = string.Empty;
        IsProductFormSuccess = false;
        await LoadPriceHistoryAsync(selected);
    }

    /// <summary>
    /// Carrega as mudanças de custo do produto em edição, da mais recente para a
    /// mais antiga, com a variação até o preço que veio depois de cada uma.
    /// </summary>
    private async Task LoadPriceHistoryAsync(PcComponent component)
    {
        PriceHistory.Clear();
        OnPropertyChanged(nameof(HasPriceHistory));

        IReadOnlyList<PriceHistoryEntry> entries;
        try
        {
            entries = await _catalogRepository.GetPriceHistoryAsync(component.Id);
        }
        catch (SqliteException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        // O primeiro registro é comparado com o preço atual; os seguintes, com o
        // registro imediatamente mais recente.
        var priceAfter = component.Price;
        foreach (var entry in entries)
        {
            PriceHistory.Add(new PriceHistoryItemViewModel(entry, priceAfter));
            priceAfter = entry.Price;
        }

        OnPropertyChanged(nameof(HasPriceHistory));
        OnPropertyChanged(nameof(PriceHistoryCountText));
    }

    private void RemoveProductImage() => ProductImagePath = string.Empty;

    private void CancelProductEdit()
    {
        var previousId = _editingProductId;
        SetEditingProductId(null);
        ClearProductForm();
        ProductFormMessage = string.Empty;
        IsProductFormSuccess = false;
        if (IsProductManagementView)
        {
            SelectCatalogProduct(null);
            ShowToolView("products");
            return;
        }

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

    private async Task ConfirmDeleteProductAsync()
    {
        var selected = SelectedCatalogProduct;
        if (selected is null)
        {
            return;
        }

        var removedImage = selected.Component.ImageUrl;
        try
        {
            if (!await _catalogRepository.DeleteAsync(selected.Id))
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
        catch (InvalidOperationException)
        {
            ProductFormMessage = "Não foi possível excluir o produto no servidor.";
            IsProductFormSuccess = false;
            return;
        }

        _productImages.DeleteIfUnused(removedImage);
        SelectCatalogProduct(null);
        SetEditingProductId(null);
        ClearProductForm();
        await RefreshCatalogCollectionsAsync();
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

    /// <summary>
    /// Uma única varredura de <see cref="Products"/> alimenta a lista filtrada e
    /// as contagens por categoria. Antes eram duas varreduras por categoria, ou
    /// seja treze passagens completas sobre o catálogo a cada tecla digitada.
    /// Cada produto usa o texto pesquisável já calculado na criação do item.
    /// </summary>
    private void RefreshProductFilter()
    {
        var showFilteredCount = !string.IsNullOrWhiteSpace(CatalogSearchText);
        var totalCounts = new Dictionary<ComponentCategory, int>();
        var filteredCounts = new Dictionary<ComponentCategory, int>();
        var matched = new List<ProductListItemViewModel>();

        foreach (var product in Products)
        {
            var category = product.Component.Category;
            totalCounts[category] = totalCounts.GetValueOrDefault(category) + 1;

            if (!ProductFilter.Matches(product.SearchableText, CatalogSearchText))
            {
                continue;
            }

            filteredCounts[category] = filteredCounts.GetValueOrDefault(category) + 1;
            if (SelectedCatalogCategoryFilter.Value is null ||
                SelectedCatalogCategoryFilter.Value == category)
            {
                matched.Add(product);
            }
        }

        var visibleProducts = ProductCatalogSorter.Sort(
            matched,
            SelectedCatalogSort.Mode,
            product => CatalogDisplayPrice(product.Component));
        FilteredProducts.Clear();
        for (var index = 0; index < visibleProducts.Count; index++)
        {
            var product = visibleProducts[index];
            product.SetAlternate(index % 2 == 1);
            FilteredProducts.Add(product);
        }

        foreach (var category in ProductCategoryFilters)
        {
            var totalCount = category.Value is null
                ? Products.Count
                : totalCounts.GetValueOrDefault(category.Value.Value);
            var filteredCount = !showFilteredCount
                ? totalCount
                : category.Value is null
                    ? filteredCounts.Values.Sum()
                    : filteredCounts.GetValueOrDefault(category.Value.Value);
            category.UpdateCounts(totalCount, filteredCount, showFilteredCount);
        }

        OnPropertyChanged(nameof(FilteredCatalogCountText));
        OnPropertyChanged(nameof(AreAllFilteredProductsSelected));
        OnPropertyChanged(nameof(CanExportProductPriceTable));
        OnPropertyChanged(nameof(ProductPriceTableSuggestedFileName));
    }

    /// <summary>
    /// Conteúdo CSV do que está visível na lista, respeitando categoria, filtro
    /// e ordenação escolhidos.
    /// </summary>
    public string BuildCatalogCsv() =>
        ProductCsvSerializer.BuildCsv(
            FilteredProducts.Select(product => product.Component));

    public string CatalogCsvSuggestedFileName =>
        $"catalogo-{SelectedCatalogCategoryFilter.Value?.ToString().ToLowerInvariant() ?? "todos"}.csv";

    /// <summary>
    /// Aplica um CSV ao catálogo: produtos com identificador novo são incluídos
    /// e os existentes são atualizados. Nada é apagado.
    /// </summary>
    public async Task ImportCatalogCsvAsync(string csv)
    {
        var parsed = ProductCsvSerializer.Parse(csv);
        if (parsed.Products.Count == 0)
        {
            BulkStatusMessage = parsed.HasErrors
                ? $"Nenhum produto importado. {FirstErrors(parsed)}"
                : "O arquivo não contém produtos.";
            return;
        }

        var existingIds = Products
            .Select(product => product.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        var updated = 0;
        var failed = 0;

        foreach (var product in parsed.Products)
        {
            try
            {
                if (existingIds.Contains(product.Id))
                {
                    if (await _catalogRepository.UpdateAsync(product))
                    {
                        updated++;
                    }
                }
                else
                {
                    await _catalogRepository.AddAsync(product);
                    added++;
                }
            }
            catch (SqliteException)
            {
                failed++;
            }
            catch (InvalidOperationException)
            {
                failed++;
            }
        }

        await RefreshCatalogCollectionsAsync();

        var parts = new List<string>();
        if (added > 0)
        {
            parts.Add($"{added} incluídos");
        }

        if (updated > 0)
        {
            parts.Add($"{updated} atualizados");
        }

        if (failed > 0)
        {
            parts.Add($"{failed} recusados pelo banco");
        }

        var summary = parts.Count > 0
            ? string.Join(", ", parts) + "."
            : "Nenhuma alteração.";
        BulkStatusMessage = parsed.HasErrors
            ? $"{summary} {FirstErrors(parsed)}"
            : summary;
    }

    /// <summary>
    /// Resume os problemas do arquivo sem inundar a barra de status.
    /// </summary>
    private static string FirstErrors(ProductCsvImport parsed)
    {
        const int maximum = 3;
        var shown = string.Join(" ", parsed.Errors.Take(maximum));
        return parsed.Errors.Count > maximum
            ? $"{shown} (+{parsed.Errors.Count - maximum} outros problemas)"
            : shown;
    }

    public void ReportCatalogCsvFailure() =>
        BulkStatusMessage = "Não foi possível ler ou gravar o arquivo CSV.";

    public ProductPriceTableDocument BuildProductPriceTableDocument()
    {
        var isCost = !_isCatalogShowingSalePrice;
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

    private void SetCatalogPriceMode(bool showSalePrice)
    {
        if (_isCatalogShowingSalePrice == showSalePrice)
        {
            return;
        }

        _isCatalogShowingSalePrice = showSalePrice;
        OnPropertyChanged(nameof(IsCatalogCostDisplayActive));
        OnPropertyChanged(nameof(IsCatalogSaleDisplayActive));
        OnPropertyChanged(nameof(CatalogPriceColumnTitle));
        OnPropertyChanged(nameof(ProductPriceTableSuggestedFileName));
        ProductPriceTableStatusMessage = string.Empty;
        RefreshCatalogDisplayPrices();
        RefreshProductFilter();
    }

    private void RefreshCatalogDisplayPrices()
    {
        foreach (var product in Products)
        {
            product.SetDisplayPrice(CatalogDisplayPrice(product.Component));
        }
    }

    private decimal CatalogDisplayPrice(PcComponent component) =>
        _isCatalogShowingSalePrice
            ? FlexibleListItemViewModel.CalculateSalePrice(
                component.Price,
                _businessSettings.MarginFor(component.Category))
            : component.Price;

    private void RequestBulkDelete()
    {
        if (HasBulkSelection)
        {
            IsBulkDeleteConfirmationVisible = true;
        }
    }

    private void CancelBulkDelete() => IsBulkDeleteConfirmationVisible = false;

    private async Task ConfirmBulkDeleteAsync()
    {
        var selectedProducts = Products
            .Where(product => product.IsBulkSelected)
            .Select(product => product.Component)
            .ToList();
        var selectedIds = selectedProducts.Select(product => product.Id).ToList();
        if (selectedIds.Count == 0)
        {
            IsBulkDeleteConfirmationVisible = false;
            return;
        }

        try
        {
            var deleted = await _catalogRepository.DeleteManyAsync(selectedIds);
            _productImages.DeleteIfUnused(selectedProducts);
            IsBulkDeleteConfirmationVisible = false;
            await RefreshCatalogCollectionsAsync();
            BulkStatusMessage = deleted == 1
                ? "1 produto apagado."
                : $"{deleted} produtos apagados.";
        }
        catch (SqliteException)
        {
            BulkStatusMessage = "Não foi possível apagar os produtos selecionados.";
        }
        catch (InvalidOperationException)
        {
            BulkStatusMessage =
                "Não foi possível apagar os produtos selecionados no servidor.";
        }
    }

    private async Task ApplyBulkDescriptionAsync()
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
            var updated = await _catalogRepository.UpdateDescriptionsAsync(
                selectedIds,
                BulkDescriptionText,
                SelectedBulkDescriptionOperation.Mode);
            await RefreshCatalogCollectionsAsync();
            BulkDescriptionText = string.Empty;
            BulkStatusMessage = updated == 1
                ? "Descrição de 1 produto atualizada."
                : $"Descrições de {updated} produtos atualizadas.";
        }
        catch (SqliteException)
        {
            BulkStatusMessage = "Não foi possível atualizar as descrições no banco de dados.";
        }
        catch (InvalidOperationException)
        {
            BulkStatusMessage = "Não foi possível atualizar as descrições no servidor.";
        }
    }

    private async Task SaveProductAsync()
    {
        var returnToProductCatalog = IsProductManagementView;
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
            imageUrl = _productImages.Persist(componentId, ProductImagePath);
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
                await _catalogRepository.AddAsync(component);
            }
            else if (!await _catalogRepository.UpdateAsync(component))
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
        catch (InvalidOperationException)
        {
            ProductFormMessage = "Não foi possível salvar o produto no servidor.";
            return;
        }

        // A foto anterior deixou de ser referenciada quando o produto passou a
        // apontar para outra.
        _productImages.DeleteIfUnused(existing?.ImageUrl, imageUrl);

        var savedId = component.Id;
        SetEditingProductId(null);
        await RefreshCatalogCollectionsAsync();
        ClearProductForm();
        SelectCatalogProduct(IsProductManagementView
            ? null
            : Products.FirstOrDefault(product =>
                string.Equals(product.Id, savedId, StringComparison.OrdinalIgnoreCase)));
        IsProductFormSuccess = true;
        ProductFormMessage = existing is null
            ? "Produto cadastrado e adicionado à montagem."
            : "Alterações do produto salvas.";
        if (returnToProductCatalog)
        {
            ShowToolView("products");
        }
    }

    private void ClearProductForm()
    {
        PriceHistory.Clear();
        OnPropertyChanged(nameof(HasPriceHistory));
        ProductName = string.Empty;
        ProductBrand = string.Empty;
        ProductDescription = string.Empty;
        ProductPrice = string.Empty;
        ProductImagePath = string.Empty;
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
}
