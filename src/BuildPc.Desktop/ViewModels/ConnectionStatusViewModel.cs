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

    public bool IsOffline => !IsOnline;
    public string StatusText => IsOnline ? "ONLINE" : "OFFLINE";

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
