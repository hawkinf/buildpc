using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using BuildPc.Core.Models;
using BuildPc.Desktop.Services;

namespace BuildPc.Desktop.ViewModels;

public sealed class FlexibleListViewModel : ViewModelBase
{
    private readonly List<PcComponent> _catalog;
    private readonly Func<FlexibleListViewModel, Task<SavedQuote?>>? _saveQuote;
    private CategoryOptionViewModel _selectedCategory;
    private int _quantity = 1;
    private BusinessSettings _settings;
    private string _clientName = string.Empty;
    private string _clientPhone = string.Empty;
    private string _notes = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isStatusSuccess;
    private bool _isSensitiveTotalsVisible;
    private bool _isDirty;
    private bool _isClearConfirmationVisible;
    private SavedQuote? _savedQuote;
    private string _discountText = string.Empty;
    private decimal _discountValue;
    private int _validityDays;
    private string _paymentTerms = string.Empty;
    private string _deliveryTerms = string.Empty;

    public FlexibleListViewModel(
        IEnumerable<PcComponent> catalog,
        IEnumerable<CategoryOptionViewModel> categories,
        BusinessSettings? settings = null,
        Func<FlexibleListViewModel, Task<SavedQuote?>>? saveQuote = null)
    {
        _catalog = catalog.ToList();
        Categories = new ObservableCollection<CategoryOptionViewModel>(categories);
        _settings = settings ?? new BusinessSettings();
        _saveQuote = saveQuote;
        _selectedCategory = Categories[0];
        Items = [];
        ProductPicker = new ComponentSlotViewModel(
            "flexible-list-draft",
            _selectedCategory.Value,
            "Produto",
            "Escolha um produto do catálogo",
            "Products",
            ProductsForSelectedCategory(),
            _ => DraftSelectionChanged(),
            SellingPriceFor);
        AddCommand = new RelayCommand(Add);
        RequestClearCommand = new RelayCommand(RequestClear);
        ConfirmClearCommand = new RelayCommand(Clear);
        CancelClearCommand = new RelayCommand(CancelClear);
        SaveQuoteCommand = new AsyncRelayCommand(SaveQuoteAsync);
    }

    public ObservableCollection<CategoryOptionViewModel> Categories { get; }
    public ObservableCollection<FlexibleListItemViewModel> Items { get; }
    public ComponentSlotViewModel ProductPicker { get; }
    public ICommand AddCommand { get; }
    public ICommand RequestClearCommand { get; }
    public ICommand ConfirmClearCommand { get; }
    public ICommand CancelClearCommand { get; }
    public ICommand SaveQuoteCommand { get; }

    public CategoryOptionViewModel SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (value is not null && SetProperty(ref _selectedCategory, value))
            {
                ProductPicker.Selected = null;
                ProductPicker.FilterText = string.Empty;
                ProductPicker.ReplaceOptions(ProductsForSelectedCategory());
                OnPropertyChanged(nameof(CategoryProductsText));
                OnPropertyChanged(nameof(CanAdd));
            }
        }
    }

    public int Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, QuantityRange.Clamp(value));
    }

    public bool CanAdd => ProductPicker.Selected is not null;
    public bool HasItems => Items.Count > 0;
    public bool IsEmpty => Items.Count == 0;

    public bool IsClearConfirmationVisible
    {
        get => _isClearConfirmationVisible;
        private set => SetProperty(ref _isClearConfirmationVisible, value);
    }

    public string ClearConfirmationMessage =>
        SavedQuote is null
            ? "Descartar esta montagem? Os itens e os dados do cliente serão " +
              "perdidos e o orçamento não está gravado."
            : $"Fechar o orçamento #{SavedQuote.Number:000000} e limpar a " +
              "montagem? O orçamento gravado continua na lista de Orçamentos.";
    public bool IsSensitiveTotalsVisible
    {
        get => _isSensitiveTotalsVisible;
        private set => SetProperty(ref _isSensitiveTotalsVisible, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(SaveStateText));
            }
        }
    }

    public SavedQuote? SavedQuote
    {
        get => _savedQuote;
        private set
        {
            if (SetProperty(ref _savedQuote, value))
            {
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(SaveStateText));
            }
        }
    }

    public bool CanExport => SavedQuote is not null && !IsDirty;
    public string SaveStateText => SavedQuote is null
        ? "Grave o orçamento antes de exportar."
        : IsDirty
            ? "Existem alterações. Grave novamente para exportar."
            : $"Orçamento #{SavedQuote.Number:000000} gravado.";

    /// <summary>Desconto digitado, em reais.</summary>
    public string DiscountText
    {
        get => _discountText;
        set
        {
            if (!SetProperty(ref _discountText, value ?? string.Empty))
            {
                return;
            }

            _discountValue = decimal.TryParse(
                _discountText,
                NumberStyles.Currency,
                MainWindowViewModel.BrazilianCulture,
                out var discount) && discount > 0
                ? discount
                : 0m;
            OnPropertyChanged(nameof(DiscountValue));
            OnPropertyChanged(nameof(FinalPriceValue));
            OnPropertyChanged(nameof(FinalPrice));
            OnPropertyChanged(nameof(HasDiscount));
            MarkDirty();
        }
    }

    public decimal DiscountValue => _discountValue;
    public bool HasDiscount => DiscountValue > 0m;

    /// <summary>Total que o cliente paga, já com o desconto aplicado.</summary>
    public decimal FinalPriceValue =>
        Math.Max(0m, TotalPriceValue - DiscountValue);

    public string FinalPrice =>
        FinalPriceValue.ToString("C", MainWindowViewModel.BrazilianCulture);

    /// <summary>Dias de validade da proposta. Zero significa sem prazo.</summary>
    public int ValidityDays
    {
        get => _validityDays;
        set
        {
            if (SetProperty(ref _validityDays, Math.Clamp(value, 0, 365)))
            {
                MarkDirty();
            }
        }
    }

    public string PaymentTerms
    {
        get => _paymentTerms;
        set
        {
            if (SetProperty(ref _paymentTerms, value ?? string.Empty))
            {
                MarkDirty();
            }
        }
    }

    public string DeliveryTerms
    {
        get => _deliveryTerms;
        set
        {
            if (SetProperty(ref _deliveryTerms, value ?? string.Empty))
            {
                MarkDirty();
            }
        }
    }

    public string ClientName
    {
        get => _clientName;
        set
        {
            if (SetProperty(ref _clientName, value ?? string.Empty))
            {
                MarkDirty();
            }
        }
    }

    public string ClientPhone
    {
        get => _clientPhone;
        set
        {
            if (SetProperty(
                    ref _clientPhone,
                    PhoneNumberFormatter.FormatBrazilian(value)))
            {
                MarkDirty();
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set
        {
            if (SetProperty(ref _notes, value ?? string.Empty))
            {
                MarkDirty();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsStatusSuccess
    {
        get => _isStatusSuccess;
        private set => SetProperty(ref _isStatusSuccess, value);
    }

    public int TotalItems => Items.Sum(item => item.Quantity);
    public decimal TotalCostValue => Items.Sum(item => item.UnitPriceValue * item.Quantity);
    public decimal TotalPriceValue => Items.Sum(item => item.SellingUnitPriceValue * item.Quantity);
    public decimal TotalProfitValue => TotalPriceValue - TotalCostValue;
    public decimal TotalProfitPercentValue => TotalCostValue <= 0
        ? 0
        : decimal.Round(
            TotalProfitValue / TotalCostValue * 100m,
            2,
            MidpointRounding.AwayFromZero);
    public string TotalCost =>
        TotalCostValue.ToString("C", MainWindowViewModel.BrazilianCulture);
    public string TotalPrice =>
        TotalPriceValue.ToString("C", MainWindowViewModel.BrazilianCulture);
    public string TotalProfit =>
        TotalProfitValue.ToString("C", MainWindowViewModel.BrazilianCulture);
    public string TotalProfitPercent =>
        $"{TotalProfitPercentValue.ToString("N2", MainWindowViewModel.BrazilianCulture)}%";
    public string ItemsText => TotalItems == 1 ? "1 produto" : $"{TotalItems} produtos";
    public string LinesText => Items.Count == 1 ? "1 linha adicionada" : $"{Items.Count} linhas adicionadas";
    public string CategoryProductsText
    {
        get
        {
            var count = ProductsForSelectedCategory().Count();
            return count == 1 ? "1 produto disponível" : $"{count} produtos disponíveis";
        }
    }

    public void UpdateCatalog(IEnumerable<PcComponent> catalog)
    {
        _catalog.Clear();
        _catalog.AddRange(catalog);
        ProductPicker.Selected = null;
        ProductPicker.ReplaceOptions(ProductsForSelectedCategory());
        OnPropertyChanged(nameof(CategoryProductsText));
        OnPropertyChanged(nameof(CanAdd));
    }

    public void UpdateCategories(IEnumerable<CategoryOptionViewModel> categories)
    {
        var previousCategory = SelectedCategory.Value;
        Categories.Clear();
        foreach (var category in categories)
        {
            Categories.Add(category);
        }

        var selected = Categories.FirstOrDefault(category =>
                           category.Value == previousCategory) ??
                       Categories.First();
        _selectedCategory = selected;
        OnPropertyChanged(nameof(SelectedCategory));
        ProductPicker.Selected = null;
        ProductPicker.ReplaceOptions(ProductsForSelectedCategory());
        OnPropertyChanged(nameof(CategoryProductsText));
        OnPropertyChanged(nameof(CanAdd));

        foreach (var item in Items)
        {
            var category = Categories.FirstOrDefault(option =>
                option.Value == item.Component.Category);
            if (category is not null)
            {
                item.SetCategoryName(category.Name);
            }
        }
    }

    private IEnumerable<PcComponent> ProductsForSelectedCategory() =>
        _catalog.Where(component => component.Category == SelectedCategory.Value);

    private void Add()
    {
        var component = ProductPicker.Selected;
        if (component is null)
        {
            return;
        }

        Items.Add(new FlexibleListItemViewModel(
            component,
            SelectedCategory.Name,
            Quantity,
            _settings.MarginFor(component.Category),
            Remove,
            ItemChanged));
        RefreshAlternatingRows();
        ProductPicker.Selected = null;
        Quantity = 1;
        MarkDirty();
        RefreshSummary();
    }

    private void Remove(FlexibleListItemViewModel item)
    {
        if (!Items.Remove(item))
        {
            return;
        }

        RefreshAlternatingRows();
        MarkDirty();
        RefreshSummary();
    }

    /// <summary>
    /// Limpar descarta um orçamento inteiro, então passa pela mesma confirmação
    /// exigida das outras ações destrutivas do programa.
    /// </summary>
    private void RequestClear()
    {
        if (HasItems || !string.IsNullOrWhiteSpace(ClientName))
        {
            OnPropertyChanged(nameof(ClearConfirmationMessage));
            IsClearConfirmationVisible = true;
        }
    }

    private void CancelClear() => IsClearConfirmationVisible = false;

    private void Clear()
    {
        IsClearConfirmationVisible = false;
        Items.Clear();
        ClientName = string.Empty;
        ClientPhone = string.Empty;
        Notes = string.Empty;
        DiscountText = string.Empty;
        ValidityDays = 0;
        PaymentTerms = string.Empty;
        DeliveryTerms = string.Empty;
        ProductPicker.Selected = null;
        ProductPicker.FilterText = string.Empty;
        Quantity = 1;
        SavedQuote = null;
        IsDirty = false;
        StatusMessage = string.Empty;
        RefreshSummary();
    }

    /// <summary>
    /// Recarrega um orçamento gravado na Montagem para consulta, ajuste ou
    /// nova gravação sobre o mesmo número.
    /// </summary>
    public void LoadQuote(SavedQuote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);

        IsClearConfirmationVisible = false;
        Items.Clear();
        foreach (var savedItem in quote.Items)
        {
            Items.Add(new FlexibleListItemViewModel(
                ResolveComponent(savedItem),
                savedItem,
                CategoryNameFor(savedItem),
                Remove,
                ItemChanged));
        }

        RefreshAlternatingRows();
        ClientName = quote.ClientName;
        ClientPhone = quote.ClientPhone;
        Notes = quote.Notes;
        DiscountText = quote.DiscountAmount > 0m
            ? quote.DiscountAmount.ToString(
                "N2",
                MainWindowViewModel.BrazilianCulture)
            : string.Empty;
        ValidityDays = quote.ValidityDays;
        PaymentTerms = quote.PaymentTerms;
        DeliveryTerms = quote.DeliveryTerms;
        ProductPicker.Selected = null;
        Quantity = 1;
        SavedQuote = quote;

        // As atribuições acima marcam a montagem como alterada; recarregar um
        // orçamento gravado não é uma alteração pendente.
        IsDirty = false;
        IsStatusSuccess = true;
        StatusMessage =
            $"Orçamento #{quote.Number:000000} aberto para consulta e ajuste.";
        RefreshSummary();
    }

    /// <summary>
    /// Carrega os itens e as condições de um orçamento como uma nova montagem,
    /// sem vínculo com o número original.
    /// </summary>
    /// <remarks>
    /// Gravar depois disto cria um orçamento novo: é o caminho para repetir uma
    /// venda para outro cliente sem remontar tudo.
    /// </remarks>
    public void LoadQuoteAsCopy(SavedQuote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        LoadQuote(quote);

        // Sem SavedQuote a gravação gera um número novo, e o cliente é limpo
        // porque uma cópia costuma ir para outra pessoa.
        SavedQuote = null;
        ClientName = string.Empty;
        ClientPhone = string.Empty;
        IsDirty = true;
        IsStatusSuccess = false;
        StatusMessage =
            $"Cópia do orçamento #{quote.Number:000000}. " +
            "Informe o cliente e grave para criar um novo número.";
    }

    /// <summary>
    /// Usa o produto atual do catálogo quando ele ainda existe, para manter
    /// foto e marca. Se tiver sido excluído, reconstrói o mínimo a partir do
    /// próprio orçamento, para a linha continuar utilizável.
    /// </summary>
    private PcComponent ResolveComponent(SavedQuoteItem savedItem) =>
        _catalog.FirstOrDefault(component =>
            string.Equals(
                component.Id,
                savedItem.ComponentId,
                StringComparison.OrdinalIgnoreCase)) ??
        new PcComponent
        {
            Id = savedItem.ComponentId,
            Category = savedItem.Category,
            Name = savedItem.Name,
            Brand = string.Empty,
            Description = savedItem.Description,
            Price = savedItem.UnitCost,
            ImageUrl = savedItem.ImageUrl
        };

    private string CategoryNameFor(SavedQuoteItem savedItem) =>
        Categories
            .FirstOrDefault(category => category.Value == savedItem.Category)?
            .Name ??
        (string.IsNullOrWhiteSpace(savedItem.CategoryName)
            ? savedItem.Category.ToString()
            : savedItem.CategoryName);

    private void DraftSelectionChanged()
    {
        OnPropertyChanged(nameof(CanAdd));
    }

    private void RefreshAlternatingRows()
    {
        for (var index = 0; index < Items.Count; index++)
        {
            Items[index].IsAlternate = index % 2 == 1;
        }
    }

    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(TotalItems));
        OnPropertyChanged(nameof(TotalCostValue));
        OnPropertyChanged(nameof(TotalCost));
        OnPropertyChanged(nameof(TotalPriceValue));
        OnPropertyChanged(nameof(TotalPrice));
        OnPropertyChanged(nameof(FinalPriceValue));
        OnPropertyChanged(nameof(FinalPrice));
        OnPropertyChanged(nameof(TotalProfitValue));
        OnPropertyChanged(nameof(TotalProfit));
        OnPropertyChanged(nameof(TotalProfitPercentValue));
        OnPropertyChanged(nameof(TotalProfitPercent));
        OnPropertyChanged(nameof(ItemsText));
        OnPropertyChanged(nameof(LinesText));
    }

    public void ApplySettings(BusinessSettings settings)
    {
        _settings = settings;
        foreach (var item in Items)
        {
            item.ApplyMargin(settings.MarginFor(item.Component.Category));
        }

        ProductPicker.ReplaceOptions(ProductsForSelectedCategory());
        if (Items.Count > 0)
        {
            MarkDirty();
        }

        RefreshSummary();
    }

    public void SetSensitiveTotalsVisible(bool visible)
    {
        IsSensitiveTotalsVisible = visible;
        foreach (var item in Items)
        {
            item.SetCostVisible(visible);
        }
    }

    public void CompletePdfPreview(bool opened)
    {
        IsStatusSuccess = opened;
        StatusMessage = opened
            ? "PDF aberto. Use o visualizador para salvar ou imprimir."
            : "O PDF foi gerado, mas não foi possível abri-lo automaticamente.";
    }

    public void FailPdfPreview()
    {
        IsStatusSuccess = false;
        StatusMessage = "Não foi possível gerar a visualização do PDF.";
    }

    /// <summary>
    /// Monta os dados do orçamento a gravar, incluindo desconto, validade e
    /// condições comerciais informados nesta tela.
    /// </summary>
    public QuoteDraft BuildQuoteDraft(BusinessSettings companySnapshot) =>
        new()
        {
            ClientName = ClientName,
            ClientPhone = ClientPhone,
            Notes = Notes,
            Items = BuildQuoteItems(),
            CompanySnapshot = companySnapshot,
            DiscountAmount = DiscountValue,
            ValidityDays = ValidityDays,
            PaymentTerms = PaymentTerms,
            DeliveryTerms = DeliveryTerms
        };

    /// <summary>Itens da montagem no formato de um modelo reutilizável.</summary>
    public IReadOnlyList<AssemblyTemplateItem> BuildTemplateItems() =>
        Items.Select(item => new AssemblyTemplateItem
        {
            ComponentId = item.Component.Id,
            Category = item.Component.Category,
            Quantity = item.Quantity
        }).ToList();

    public IReadOnlyList<SavedQuoteItem> BuildQuoteItems() =>
        Items.Select(item => new SavedQuoteItem
        {
            ComponentId = item.Component.Id,
            Category = item.Component.Category,
            CategoryName = item.CategoryName,
            Name = item.Name.Trim(),
            Description = item.Description.Trim(),
            ImageUrl = item.ImageUrl,
            Quantity = item.Quantity,
            UnitCost = item.UnitPriceValue,
            MarginPercent = item.MarginPercent,
            UnitPrice = item.SellingUnitPriceValue
        }).ToList();

    private async Task SaveQuoteAsync()
    {
        if (!HasItems)
        {
            Fail("Adicione ao menos um produto antes de gravar.");
            return;
        }

        if (string.IsNullOrWhiteSpace(ClientName) ||
            string.IsNullOrWhiteSpace(ClientPhone))
        {
            Fail("Informe o nome e o telefone do cliente.");
            return;
        }

        if (_saveQuote is null)
        {
            Fail("O armazenamento de orçamentos não está disponível.");
            return;
        }

        var saved = await _saveQuote(this);
        if (saved is null)
        {
            Fail("Não foi possível gravar o orçamento.");
            return;
        }

        SavedQuote = saved;
        IsDirty = false;
        IsStatusSuccess = true;
        StatusMessage = $"Orçamento #{saved.Number:000000} gravado em " +
                        $"{saved.CreatedAt.LocalDateTime:dd/MM/yyyy HH:mm}.";
    }

    private void ItemChanged()
    {
        MarkDirty();
        RefreshSummary();
    }

    private void MarkDirty()
    {
        IsDirty = true;
        IsStatusSuccess = false;
        StatusMessage = string.Empty;
    }

    private void Fail(string message)
    {
        IsStatusSuccess = false;
        StatusMessage = message;
    }

    private decimal SellingPriceFor(PcComponent component) =>
        FlexibleListItemViewModel.CalculateSalePrice(
            component.Price,
            _settings.MarginFor(component.Category));
}
