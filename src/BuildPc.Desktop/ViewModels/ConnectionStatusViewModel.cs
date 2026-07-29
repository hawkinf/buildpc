using BuildPc.Core.Services;

namespace BuildPc.Desktop.ViewModels;

public sealed class ConnectionStatusViewModel : ViewModelBase
{
    private readonly BuildPcApiSettings? _settings;
    private readonly Func<BuildPcApiSettings, Task> _testConnection;
    private bool _isRefreshing;
    private bool _isOnline;

    public ConnectionStatusViewModel(
        BuildPcApiSettings? settings,
        Func<BuildPcApiSettings, Task> testConnection)
    {
        _settings = settings;
        _testConnection = testConnection;
        _isOnline = settings is not null;
    }

    /// <summary>
    /// Verdadeiro quando o programa usa o banco SQLite deste computador. Nesse
    /// modo não existe servidor para estar fora do ar, então o rodapé não pode
    /// alarmar com OFFLINE em vermelho.
    /// </summary>
    public bool IsLocalDatabase => _settings is null;

    public bool IsOnline
    {
        get => _isOnline;
        private set
        {
            if (SetProperty(ref _isOnline, value))
            {
                OnPropertyChanged(nameof(IsOffline));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public bool IsOffline => !IsLocalDatabase && !IsOnline;

    public string StatusText =>
        IsLocalDatabase
            ? "LOCAL"
            : IsOnline
                ? "ONLINE"
                : "OFFLINE";

    public string StatusDescription =>
        IsLocalDatabase
            ? "Usando o banco de dados deste computador"
            : IsOnline
                ? "Conectado ao servidor de dados"
                : "Sem conexão com o servidor de dados";

    public async Task RefreshAsync()
    {
        if (_settings is null)
        {
            IsOnline = false;
            return;
        }

        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            await _testConnection(_settings);
            IsOnline = true;
        }
        catch
        {
            IsOnline = false;
        }
        finally
        {
            _isRefreshing = false;
        }
    }
}
