using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using BuildPc.Core.Models;
using BuildPc.Core.Services;
using BuildPc.Desktop.Services;

namespace BuildPc.Desktop.ViewModels;

public sealed class PricingSettingsViewModel : ViewModelBase
{
    private readonly IReadOnlyList<CategoryOptionViewModel> _categories;
    private readonly Func<BusinessSettings, Task<string?>> _save;
    private readonly Action<AppThemeMode> _applyTheme;
    private List<ProductCategoryDefinition>? _productCategories;
    private CategoryOptionViewModel? _selectedCategory;
    private ThemeModeOptionViewModel _selectedThemeOption;
    private string _newMarginText = string.Empty;
    private string _globalMarginText;
    private string _companyName;
    private string _companyDocument;
    private string _companyPhone;
    private string _companyEmail;
    private string _companyWebsite;
    private string _companyAddress;
    private string _logoPath;
    private string _additionalQuoteInfo;
    private string _statusMessage = string.Empty;
    private bool _isSuccess;

    public PricingSettingsViewModel(
        BusinessSettings settings,
        IReadOnlyList<CategoryOptionViewModel> categories,
        Func<BusinessSettings, Task<string?>> save,
        BuildPcApiSettings? apiSettings,
        Action<BuildPcApiSettings?> saveApiSettings,
        Func<BuildPcApiSettings, Task> testApiConnection,
        Action<AppThemeMode> applyTheme,
        bool isApiKeyUnreadable = false)
    {
        _categories = categories;
        _save = save;
        _applyTheme = applyTheme;
        _productCategories = settings.ProductCategories?.ToList();
        ThemeOptions =
        [
            new(AppThemeMode.System, "Sistema", "Acompanha automaticamente o tema do Windows"),
            new(AppThemeMode.Light, "Claro", "Mantém o programa sempre no modo claro"),
            new(AppThemeMode.Dark, "Escuro", "Mantém o programa sempre no modo escuro")
        ];
        _selectedThemeOption =
            ThemeOptions.FirstOrDefault(option => option.Mode == settings.ThemeMode) ??
            ThemeOptions[0];
        _applyTheme(_selectedThemeOption.Mode);
        _globalMarginText = Format(Math.Max(
            BusinessSettings.MinimumMarginPercent,
            settings.GlobalMarginPercent));
        _companyName = settings.CompanyName;
        _companyDocument = settings.CompanyDocument;
        _companyPhone = PhoneNumberFormatter.FormatBrazilian(settings.CompanyPhone);
        _companyEmail = settings.CompanyEmail;
        _companyWebsite = settings.CompanyWebsite;
        _companyAddress = settings.CompanyAddress;
        _logoPath = settings.LogoPath;
        _additionalQuoteInfo = settings.AdditionalQuoteInfo;
        ServerSettings = new DataServerSettingsViewModel(
            apiSettings,
            saveApiSettings,
            testApiConnection,
            isApiKeyUnreadable);
        CategoryMargins = [];
        foreach (var entry in settings.CategoryMargins)
        {
            var category = categories.FirstOrDefault(item => item.Value == entry.Key);
            CategoryMargins.Add(new CategoryMarginViewModel(
                entry.Key,
                category?.Name ?? entry.Key.ToString(),
                Math.Max(BusinessSettings.MinimumMarginPercent, entry.Value),
                RemoveMargin));
        }

        SelectedCategory = AvailableCategories.FirstOrDefault();
        AddMarginCommand = new RelayCommand(AddMargin);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public ObservableCollection<CategoryMarginViewModel> CategoryMargins { get; }
    public DataServerSettingsViewModel ServerSettings { get; }
    public IReadOnlyList<ThemeModeOptionViewModel> ThemeOptions { get; }
    public IReadOnlyList<CategoryOptionViewModel> AvailableCategories =>
        _categories.Where(category =>
            CategoryMargins.All(margin => margin.Category != category.Value)).ToList();
    public ICommand AddMarginCommand { get; }
    public ICommand SaveCommand { get; }

    public ThemeModeOptionViewModel SelectedThemeOption
    {
        get => _selectedThemeOption;
        set
        {
            if (value is not null && SetProperty(ref _selectedThemeOption, value))
            {
                _applyTheme(value.Mode);
                ClearStatus();
            }
        }
    }

    public CategoryOptionViewModel? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public string NewMarginText
    {
        get => _newMarginText;
        set => SetProperty(ref _newMarginText, value ?? string.Empty);
    }

    public string GlobalMarginText
    {
        get => _globalMarginText;
        set => SetProperty(ref _globalMarginText, value ?? string.Empty);
    }

    public string CompanyName { get => _companyName; set => SetProperty(ref _companyName, value ?? string.Empty); }
    public string CompanyDocument { get => _companyDocument; set => SetProperty(ref _companyDocument, value ?? string.Empty); }
    public string CompanyPhone
    {
        get => _companyPhone;
        set => SetProperty(
            ref _companyPhone,
            PhoneNumberFormatter.FormatBrazilian(value));
    }
    public string CompanyEmail { get => _companyEmail; set => SetProperty(ref _companyEmail, value ?? string.Empty); }
    public string CompanyWebsite { get => _companyWebsite; set => SetProperty(ref _companyWebsite, value ?? string.Empty); }
    public string CompanyAddress { get => _companyAddress; set => SetProperty(ref _companyAddress, value ?? string.Empty); }
    public string LogoPath { get => _logoPath; set => SetProperty(ref _logoPath, value ?? string.Empty); }
    public string AdditionalQuoteInfo { get => _additionalQuoteInfo; set => SetProperty(ref _additionalQuoteInfo, value ?? string.Empty); }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        private set => SetProperty(ref _isSuccess, value);
    }

    public bool IsError => !IsSuccess && !string.IsNullOrWhiteSpace(StatusMessage);

    private void AddMargin()
    {
        if (SelectedCategory is null ||
            !TryParsePercent(NewMarginText, out var margin))
        {
            Fail("Selecione uma categoria e informe uma margem de pelo menos 15%.");
            return;
        }

        CategoryMargins.Add(new CategoryMarginViewModel(
            SelectedCategory.Value,
            SelectedCategory.Name,
            margin,
            RemoveMargin));
        NewMarginText = string.Empty;
        OnPropertyChanged(nameof(AvailableCategories));
        SelectedCategory = AvailableCategories.FirstOrDefault();
        ClearStatus();
    }

    private void RemoveMargin(CategoryMarginViewModel margin)
    {
        CategoryMargins.Remove(margin);
        OnPropertyChanged(nameof(AvailableCategories));
        SelectedCategory ??= AvailableCategories.FirstOrDefault();
        ClearStatus();
    }

    private async Task SaveAsync()
    {
        if (!TryParsePercent(GlobalMarginText, out var globalMargin))
        {
            Fail("A margem de lucro global deve ser de pelo menos 15%.");
            return;
        }

        var categoryMargins = new Dictionary<ComponentCategory, decimal>();
        foreach (var margin in CategoryMargins)
        {
            if (!margin.TryGetMargin(out var value))
            {
                Fail($"A margem de {margin.CategoryName} deve ser de pelo menos 15%.");
                return;
            }

            categoryMargins[margin.Category] = value;
        }

        var settings = new BusinessSettings
        {
            GlobalMarginPercent = globalMargin,
            CategoryMargins = categoryMargins,
            ProductCategories = _productCategories?.ToList(),
            CompanyName = CompanyName.Trim(),
            CompanyDocument = CompanyDocument.Trim(),
            CompanyPhone = CompanyPhone.Trim(),
            CompanyEmail = CompanyEmail.Trim(),
            CompanyWebsite = CompanyWebsite.Trim(),
            CompanyAddress = CompanyAddress.Trim(),
            LogoPath = LogoPath.Trim(),
            AdditionalQuoteInfo = AdditionalQuoteInfo.Trim(),
            ThemeMode = SelectedThemeOption.Mode
        };
        var error = await _save(settings);
        if (error is not null)
        {
            Fail(error);
            return;
        }

        IsSuccess = true;
        StatusMessage = "Configurações salvas e margens aplicadas à montagem.";
        OnPropertyChanged(nameof(IsError));
    }

    public void RefreshCategories(
        IReadOnlyList<ProductCategoryDefinition> categories)
    {
        _productCategories = categories.ToList();
        var activeCategories = categories
            .Select(category => category.Value)
            .ToHashSet();
        foreach (var margin in CategoryMargins
                     .Where(margin => !activeCategories.Contains(margin.Category))
                     .ToList())
        {
            CategoryMargins.Remove(margin);
        }

        foreach (var margin in CategoryMargins)
        {
            var category = _categories.FirstOrDefault(option =>
                option.Value == margin.Category);
            if (category is not null)
            {
                margin.SetCategoryName(category.Name);
            }
        }

        var selectedCategoryValue = SelectedCategory?.Value;
        SelectedCategory = selectedCategoryValue is null
            ? null
            : _categories.FirstOrDefault(category =>
                category.Value == selectedCategoryValue);

        OnPropertyChanged(nameof(AvailableCategories));
        SelectedCategory ??= AvailableCategories.FirstOrDefault();
    }

    private void Fail(string message)
    {
        IsSuccess = false;
        StatusMessage = message;
        OnPropertyChanged(nameof(IsError));
    }

    private void ClearStatus()
    {
        IsSuccess = false;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(IsError));
    }

    private static bool TryParsePercent(string text, out decimal margin) =>
        decimal.TryParse(
            text,
            NumberStyles.Number,
            MainWindowViewModel.BrazilianCulture,
            out margin) &&
        margin >= BusinessSettings.MinimumMarginPercent;

    private static string Format(decimal value) =>
        value.ToString("N2", MainWindowViewModel.BrazilianCulture);
}
