using System.Collections.ObjectModel;
using System.Windows.Input;
using BuildPc.Core.Models;
using BuildPc.Core.Services;
using Microsoft.Data.Sqlite;

namespace BuildPc.Desktop.ViewModels;

public sealed class AssemblyTemplateItemViewModel(AssemblyTemplate template)
{
    public AssemblyTemplate Template { get; } = template;
    public string Name { get; } = template.Name;
    public string Description { get; } = template.Description;

    public string ItemsText { get; } = template.Items.Count == 1
        ? "1 produto"
        : $"{template.Items.Count} produtos";

    public string QuantityText { get; } = template.TotalItems == 1
        ? "1 unidade no total"
        : $"{template.TotalItems} unidades no total";

    public bool HasDescription { get; } =
        !string.IsNullOrWhiteSpace(template.Description);
}

/// <summary>
/// Modelos de montagem reutilizáveis, para não remontar as combinações mais
/// vendidas item por item.
/// </summary>
/// <remarks>
/// Um modelo guarda apenas os identificadores e as quantidades. Ao aplicar, os
/// valores vêm do catálogo atual — é o que o distingue de um orçamento gravado,
/// que congela os valores acordados.
/// </remarks>
public sealed class AssemblyTemplateListViewModel : ViewModelBase
{
    private readonly IAssemblyTemplateRepository _repository;
    private readonly Func<AssemblyTemplate, Task> _apply;
    private readonly Func<IReadOnlyList<AssemblyTemplateItem>> _currentItems;
    private AssemblyTemplateItemViewModel? _selectedTemplate;
    private string _newTemplateName = string.Empty;
    private string _newTemplateDescription = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isSuccess;
    private bool _isDeleteConfirmationVisible;

    public AssemblyTemplateListViewModel(
        IAssemblyTemplateRepository repository,
        Func<AssemblyTemplate, Task> apply,
        Func<IReadOnlyList<AssemblyTemplateItem>> currentItems)
    {
        _repository = repository;
        _apply = apply;
        _currentItems = currentItems;
        Templates = [];
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ApplyCommand = new AsyncRelayCommand(ApplySelectedAsync);
        RequestDeleteCommand = new RelayCommand(RequestDelete);
        ConfirmDeleteCommand = new AsyncRelayCommand(ConfirmDeleteAsync);
        CancelDeleteCommand = new RelayCommand(CancelDelete);
    }

    public ObservableCollection<AssemblyTemplateItemViewModel> Templates { get; }
    public ICommand SaveCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand RequestDeleteCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }

    public AssemblyTemplateItemViewModel? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value))
            {
                IsDeleteConfirmationVisible = false;
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(DeleteConfirmationMessage));
            }
        }
    }

    public bool HasSelection => SelectedTemplate is not null;
    public bool HasTemplates => Templates.Count > 0;
    public bool IsEmpty => !HasTemplates;

    public string NewTemplateName
    {
        get => _newTemplateName;
        set
        {
            if (SetProperty(ref _newTemplateName, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public string NewTemplateDescription
    {
        get => _newTemplateDescription;
        set => SetProperty(ref _newTemplateDescription, value ?? string.Empty);
    }

    public bool CanSave => !string.IsNullOrWhiteSpace(NewTemplateName);

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatus));
                OnPropertyChanged(nameof(IsError));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

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

    public bool IsError => !IsSuccess && HasStatus;

    public bool IsDeleteConfirmationVisible
    {
        get => _isDeleteConfirmationVisible;
        private set => SetProperty(ref _isDeleteConfirmationVisible, value);
    }

    public string DeleteConfirmationMessage =>
        SelectedTemplate is null
            ? string.Empty
            : $"Excluir o modelo “{SelectedTemplate.Name}”? " +
              "Os orçamentos já gravados não são afetados.";

    public async Task RefreshAsync()
    {
        IReadOnlyList<AssemblyTemplate> templates;
        try
        {
            templates = await _repository.GetTemplatesAsync();
        }
        catch (SqliteException)
        {
            Fail("Não foi possível ler os modelos do banco local.");
            return;
        }
        catch (InvalidOperationException)
        {
            Fail("Não foi possível ler os modelos no servidor.");
            return;
        }

        var selectedId = SelectedTemplate?.Template.Id;
        Templates.Clear();
        foreach (var template in templates)
        {
            Templates.Add(new AssemblyTemplateItemViewModel(template));
        }

        SelectedTemplate =
            Templates.FirstOrDefault(item => item.Template.Id == selectedId) ??
            Templates.FirstOrDefault();
        OnPropertyChanged(nameof(HasTemplates));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            Fail("Informe um nome para o modelo.");
            return;
        }

        var items = _currentItems();
        if (items.Count == 0)
        {
            Fail("Adicione produtos à montagem antes de salvar um modelo.");
            return;
        }

        try
        {
            await _repository.SaveTemplateAsync(new AssemblyTemplate
            {
                Name = NewTemplateName,
                Description = NewTemplateDescription,
                Items = items
            });
        }
        catch (ArgumentException exception)
        {
            Fail(exception.Message);
            return;
        }
        catch (SqliteException)
        {
            Fail("Não foi possível salvar o modelo no banco local.");
            return;
        }
        catch (InvalidOperationException)
        {
            Fail("Não foi possível salvar o modelo no servidor.");
            return;
        }

        var name = NewTemplateName.Trim();
        NewTemplateName = string.Empty;
        NewTemplateDescription = string.Empty;
        await RefreshAsync();
        IsSuccess = true;
        StatusMessage = $"Modelo “{name}” salvo com {items.Count} produtos.";
    }

    private async Task ApplySelectedAsync()
    {
        if (SelectedTemplate is not { } selected)
        {
            return;
        }

        await _apply(selected.Template);
        IsSuccess = true;
        StatusMessage =
            $"Modelo “{selected.Name}” aplicado com os preços atuais do catálogo.";
    }

    private void RequestDelete()
    {
        if (SelectedTemplate is not null)
        {
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(DeleteConfirmationMessage));
            IsDeleteConfirmationVisible = true;
        }
    }

    private void CancelDelete() => IsDeleteConfirmationVisible = false;

    private async Task ConfirmDeleteAsync()
    {
        if (SelectedTemplate is not { } selected)
        {
            IsDeleteConfirmationVisible = false;
            return;
        }

        var name = selected.Name;
        try
        {
            await _repository.DeleteTemplateAsync(selected.Template.Id);
        }
        catch (SqliteException)
        {
            Fail("Não foi possível excluir o modelo do banco local.");
            IsDeleteConfirmationVisible = false;
            return;
        }
        catch (InvalidOperationException)
        {
            Fail("Não foi possível excluir o modelo no servidor.");
            IsDeleteConfirmationVisible = false;
            return;
        }

        IsDeleteConfirmationVisible = false;
        SelectedTemplate = null;
        await RefreshAsync();
        IsSuccess = true;
        StatusMessage = $"Modelo “{name}” excluído.";
    }

    private void Fail(string message)
    {
        IsSuccess = false;
        StatusMessage = message;
    }
}
