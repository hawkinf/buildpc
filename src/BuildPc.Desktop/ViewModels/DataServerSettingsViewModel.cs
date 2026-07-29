using System.Windows.Input;
using BuildPc.Core.Services;

namespace BuildPc.Desktop.ViewModels;

public sealed class DataServerSettingsViewModel : ViewModelBase
{
    private readonly Action<BuildPcApiSettings?> _save;
    private readonly Func<BuildPcApiSettings, Task> _testConnection;
    private readonly AsyncRelayCommand _testConnectionCommand;
    private bool _useServer;
    private string _serverUrl;
    private string _apiKey;
    private bool _showApiKey;
    private bool _isTesting;
    private bool _isSuccess;
    private string _statusMessage = string.Empty;

    public DataServerSettingsViewModel(
        BuildPcApiSettings? settings,
        Action<BuildPcApiSettings?> save,
        Func<BuildPcApiSettings, Task> testConnection)
    {
        _save = save;
        _testConnection = testConnection;
        _useServer = settings is not null;
        _serverUrl = settings?.BaseUrl ?? string.Empty;
        _apiKey = settings?.ApiKey ?? string.Empty;
        _testConnectionCommand = new AsyncRelayCommand(
            TestConnectionAsync,
            () => UseServer);
        TestConnectionCommand = _testConnectionCommand;
        SaveCommand = new RelayCommand(Save);
    }

    public ICommand TestConnectionCommand { get; }
    public ICommand SaveCommand { get; }

    public bool UseServer
    {
        get => _useServer;
        set
        {
            if (SetProperty(ref _useServer, value))
            {
                _testConnectionCommand.NotifyCanExecuteChanged();
                ClearStatus();
                OnPropertyChanged(nameof(UseLocalDatabase));
            }
        }
    }

    public bool UseLocalDatabase => !UseServer;

    public string ServerUrl
    {
        get => _serverUrl;
        set
        {
            if (SetProperty(ref _serverUrl, value?.Trim() ?? string.Empty))
            {
                ClearStatus();
            }
        }
    }

    public string ApiKey
    {
        get => _apiKey;
        set
        {
            if (SetProperty(ref _apiKey, value?.Trim() ?? string.Empty))
            {
                ClearStatus();
            }
        }
    }

    public bool ShowApiKey
    {
        get => _showApiKey;
        set
        {
            if (SetProperty(ref _showApiKey, value))
            {
                OnPropertyChanged(nameof(HideApiKey));
            }
        }
    }

    public bool HideApiKey => !ShowApiKey;

    public bool IsTesting
    {
        get => _isTesting;
        private set => SetProperty(ref _isTesting, value);
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        private set => SetProperty(ref _isSuccess, value);
    }

    public bool IsError => !IsSuccess && !string.IsNullOrWhiteSpace(StatusMessage);

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

    public async Task TestConnectionAsync()
    {
        if (!TryCreateSettings(out var settings))
        {
            Fail("Informe a URL HTTPS e a chave de acesso antes de testar.");
            return;
        }

        IsTesting = true;
        IsSuccess = false;
        StatusMessage = "Testando conexão...";
        try
        {
            await _testConnection(settings);
            IsSuccess = true;
            StatusMessage = "Conexão realizada com sucesso.";
        }
        catch (Exception exception)
        {
            Fail($"Não foi possível conectar: {exception.Message}");
        }
        finally
        {
            IsTesting = false;
        }
    }

    private void Save()
    {
        try
        {
            if (!UseServer)
            {
                _save(null);
                IsSuccess = true;
                StatusMessage =
                    "Banco local selecionado. Reinicie o BuildPC para aplicar.";
                return;
            }

            if (!TryCreateSettings(out var settings))
            {
                Fail("Informe uma URL HTTPS e uma chave de acesso válidas.");
                return;
            }

            _save(settings);
            IsSuccess = true;
            StatusMessage =
                "Acesso ao servidor salvo. Reinicie o BuildPC para aplicar.";
        }
        catch (Exception exception)
        {
            Fail($"Não foi possível salvar o acesso: {exception.Message}");
        }
    }

    private bool TryCreateSettings(out BuildPcApiSettings settings)
    {
        var url = ServerUrl.Trim();
        if (!url.EndsWith("/", StringComparison.Ordinal))
        {
            url += "/";
        }

        settings = new BuildPcApiSettings
        {
            BaseUrl = url,
            ApiKey = ApiKey.Trim()
        };
        return UseServer && settings.IsValid();
    }

    private void Fail(string message)
    {
        IsSuccess = false;
        StatusMessage = message;
    }

    private void ClearStatus()
    {
        IsSuccess = false;
        StatusMessage = string.Empty;
    }
}
