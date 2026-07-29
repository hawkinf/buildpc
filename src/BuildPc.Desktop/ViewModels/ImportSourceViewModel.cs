using System.Windows.Input;
using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed class ImportSourceViewModel : ViewModelBase
{
    private bool _isImporting;
    private bool _isBatchImporting;
    private string _statusMessage;
    private string _url;
    private int _importedCount;
    private DateTimeOffset? _lastImportedAt;
    private readonly Action<ImportSourceViewModel>? _configurationChanged;

    public ImportSourceViewModel(
        ComponentCategory category,
        string title,
        string subtitle,
        string icon,
        string url,
        string sourceKey,
        int importedCount,
        DateTimeOffset? lastImportedAt,
        Func<ImportSourceViewModel, Task> import,
        Action<ImportSourceViewModel>? configurationChanged = null)
    {
        Category = category;
        Title = title;
        Subtitle = subtitle;
        Icon = icon;
        _url = url;
        SourceKey = sourceKey;
        _importedCount = importedCount;
        _lastImportedAt = lastImportedAt;
        _configurationChanged = configurationChanged;
        _statusMessage = "Pronto para importar.";
        ImportCommand = new AsyncRelayCommand(() => import(this));
    }

    public ComponentCategory Category { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string Icon { get; }
    public string Url
    {
        get => _url;
        set
        {
            if (SetProperty(ref _url, value ?? string.Empty))
            {
                _configurationChanged?.Invoke(this);
            }
        }
    }
    public string SourceKey { get; }
    public ICommand ImportCommand { get; }

    public bool IsImporting
    {
        get => _isImporting;
        set
        {
            if (SetProperty(ref _isImporting, value))
            {
                OnPropertyChanged(nameof(CanImport));
                OnPropertyChanged(nameof(ButtonText));
            }
        }
    }

    public bool IsBatchImporting
    {
        get => _isBatchImporting;
        set
        {
            if (SetProperty(ref _isBatchImporting, value))
            {
                OnPropertyChanged(nameof(CanImport));
            }
        }
    }

    public bool CanImport => !IsImporting && !IsBatchImporting;
    public string ButtonText => IsImporting ? "Importando..." : $"Importar {Title.ToLowerInvariant()}";

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int ImportedCount
    {
        get => _importedCount;
        set
        {
            if (SetProperty(ref _importedCount, value))
            {
                OnPropertyChanged(nameof(ImportedCountText));
            }
        }
    }

    public string ImportedCountText =>
        ImportedCount == 1 ? "1 produto importado" : $"{ImportedCount} produtos importados";

    public DateTimeOffset? LastImportedAt
    {
        get => _lastImportedAt;
        set
        {
            if (SetProperty(ref _lastImportedAt, value))
            {
                OnPropertyChanged(nameof(LastImportText));
            }
        }
    }

    public string LastImportText => LastImportedAt is null
        ? "Última importação: ainda não realizada"
        : $"Última importação: {LastImportedAt.Value.ToLocalTime():dd/MM/yyyy 'às' HH:mm}";
}
