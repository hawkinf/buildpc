using System.Collections.ObjectModel;
using System.Windows.Input;
using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed class CategoryManagementViewModel : ViewModelBase
{
    private readonly Func<ComponentCategory, int> _productCount;
    private readonly Func<IReadOnlyList<ProductCategoryDefinition>, Task<string?>> _save;
    private CategoryManagementItemViewModel? _selectedCategory;
    private string _categoryName = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isSuccess;
    private bool _isDeleteConfirmationVisible;

    public CategoryManagementViewModel(
        IEnumerable<ProductCategoryDefinition> categories,
        Func<ComponentCategory, int> productCount,
        Func<IReadOnlyList<ProductCategoryDefinition>, Task<string?>> save)
    {
        _productCount = productCount;
        _save = save;
        Categories = [];
        Reload(categories, null);
        NewCategoryCommand = new RelayCommand(BeginNewCategory);
        SaveCategoryCommand = new AsyncRelayCommand(SaveCategoryAsync);
        CancelEditCommand = new RelayCommand(BeginNewCategory);
        RequestDeleteCategoryCommand = new RelayCommand(RequestDeleteCategory);
        ConfirmDeleteCategoryCommand = new AsyncRelayCommand(ConfirmDeleteCategoryAsync);
        CancelDeleteCategoryCommand = new RelayCommand(CancelDeleteCategory);
    }

    public ObservableCollection<CategoryManagementItemViewModel> Categories { get; }
    public ICommand NewCategoryCommand { get; }
    public ICommand SaveCategoryCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand RequestDeleteCategoryCommand { get; }
    public ICommand ConfirmDeleteCategoryCommand { get; }
    public ICommand CancelDeleteCategoryCommand { get; }

    public CategoryManagementItemViewModel? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetProperty(ref _selectedCategory, value))
            {
                return;
            }

            CategoryName = value?.Name ?? string.Empty;
            IsDeleteConfirmationVisible = false;
            ClearStatus();
            OnPropertyChanged(nameof(IsEditingCategory));
            OnPropertyChanged(nameof(EditorTitle));
            OnPropertyChanged(nameof(SaveButtonText));
            OnPropertyChanged(nameof(CanRequestDeleteCategory));
        }
    }

    public string CategoryName
    {
        get => _categoryName;
        set
        {
            if (SetProperty(ref _categoryName, value ?? string.Empty))
            {
                ClearStatus();
            }
        }
    }

    public bool IsEditingCategory => SelectedCategory is not null;
    public string EditorTitle =>
        IsEditingCategory ? "Alterar categoria" : "Adicionar categoria";
    public string SaveButtonText =>
        IsEditingCategory ? "Salvar nome" : "Adicionar categoria";
    public bool CanRequestDeleteCategory =>
        SelectedCategory is { IsSystem: false };

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        private set
        {
            if (SetProperty(ref _isSuccess, value))
            {
                OnPropertyChanged(nameof(IsError));
            }
        }
    }

    public bool IsError => !IsSuccess && !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsDeleteConfirmationVisible
    {
        get => _isDeleteConfirmationVisible;
        private set => SetProperty(ref _isDeleteConfirmationVisible, value);
    }

    public string CategoryCountText =>
        Categories.Count == 1 ? "1 categoria" : $"{Categories.Count} categorias";

    public void UpdateProductCounts()
    {
        var selectedValue = SelectedCategory?.Value;
        Reload(
            Categories.Select(category => category.Definition).ToList(),
            selectedValue);
    }

    private void BeginNewCategory()
    {
        SelectedCategory = null;
        CategoryName = string.Empty;
        IsDeleteConfirmationVisible = false;
        ClearStatus();
    }

    private async Task SaveCategoryAsync()
    {
        var name = CategoryName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            Fail("Informe o nome da categoria.");
            return;
        }

        if (Categories.Any(category =>
                !ReferenceEquals(category, SelectedCategory) &&
                string.Equals(
                    category.Name,
                    name,
                    StringComparison.CurrentCultureIgnoreCase)))
        {
            Fail("Já existe uma categoria com esse nome.");
            return;
        }

        var wasEditing = SelectedCategory is not null;
        var definitions = Categories
            .Select(category => category.Definition)
            .ToList();
        ProductCategoryDefinition saved;
        if (SelectedCategory is null)
        {
            var nextValue = Math.Max(
                1000,
                definitions.Count == 0
                    ? 1000
                    : definitions.Max(category => (int)category.Value) + 1);
            saved = new ProductCategoryDefinition
            {
                Value = (ComponentCategory)nextValue,
                Name = name,
                DisplayOrder = definitions.Count == 0
                    ? 0
                    : definitions.Max(category => category.DisplayOrder) + 1,
                IsSystem = false
            };
            definitions.Add(saved);
        }
        else
        {
            saved = SelectedCategory.Definition with { Name = name };
            var index = definitions.FindIndex(category =>
                category.Value == saved.Value);
            definitions[index] = saved;
        }

        if (!await TrySaveAsync(definitions))
        {
            return;
        }

        Reload(definitions, saved.Value);
        IsSuccess = true;
        StatusMessage = wasEditing
            ? "Categoria salva."
            : "Categoria adicionada.";
    }

    private void RequestDeleteCategory()
    {
        if (SelectedCategory is null)
        {
            return;
        }

        if (SelectedCategory.IsSystem)
        {
            Fail("Categorias do sistema podem ser renomeadas, mas não excluídas.");
            return;
        }

        if (SelectedCategory.ProductCount > 0)
        {
            Fail(
                $"A categoria possui {SelectedCategory.ProductCount} produto(s). " +
                "Mova ou exclua esses produtos antes de apagar a categoria.");
            return;
        }

        IsDeleteConfirmationVisible = true;
        ClearStatus();
    }

    private async Task ConfirmDeleteCategoryAsync()
    {
        var selected = SelectedCategory;
        if (selected is null ||
            selected.IsSystem ||
            selected.ProductCount > 0)
        {
            IsDeleteConfirmationVisible = false;
            return;
        }

        var definitions = Categories
            .Where(category => category.Value != selected.Value)
            .Select(category => category.Definition)
            .ToList();
        if (!await TrySaveAsync(definitions))
        {
            return;
        }

        Reload(definitions, null);
        IsSuccess = true;
        StatusMessage = "Categoria excluída.";
    }

    private void CancelDeleteCategory() =>
        IsDeleteConfirmationVisible = false;

    private async Task<bool> TrySaveAsync(
        IReadOnlyList<ProductCategoryDefinition> definitions)
    {
        var error = await _save(definitions);
        if (string.IsNullOrWhiteSpace(error))
        {
            return true;
        }

        Fail(error);
        return false;
    }

    private void Reload(
        IEnumerable<ProductCategoryDefinition> definitions,
        ComponentCategory? selectedValue)
    {
        _selectedCategory = null;
        Categories.Clear();
        var ordered = definitions
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            Categories.Add(new CategoryManagementItemViewModel(
                ordered[index],
                _productCount(ordered[index].Value),
                index % 2 == 1));
        }

        OnPropertyChanged(nameof(CategoryCountText));
        var selected = selectedValue is null
            ? null
            : Categories.FirstOrDefault(category =>
                category.Value == selectedValue);
        if (selected is not null)
        {
            SelectedCategory = selected;
            return;
        }

        CategoryName = string.Empty;
        IsDeleteConfirmationVisible = false;
        OnPropertyChanged(nameof(SelectedCategory));
        OnPropertyChanged(nameof(IsEditingCategory));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(CanRequestDeleteCategory));
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
}
