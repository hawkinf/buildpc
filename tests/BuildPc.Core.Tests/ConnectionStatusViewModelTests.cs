using BuildPc.Core.Services;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Core.Tests;

public sealed class ConnectionStatusViewModelTests
{
    [Fact]
    public async Task LocalDatabaseIsReportedAsLocalWithoutCallingServer()
    {
        var calls = 0;
        var viewModel = new ConnectionStatusViewModel(
            settings: null,
            _ =>
            {
                calls++;
                return Task.CompletedTask;
            });

        await viewModel.RefreshAsync();

        // Sem servidor configurado não há o que estar fora do ar: o rodapé não
        // pode alarmar com OFFLINE em vermelho no modo de uso normal.
        Assert.True(viewModel.IsLocalDatabase);
        Assert.False(viewModel.IsOnline);
        Assert.False(viewModel.IsOffline);
        Assert.Equal("LOCAL", viewModel.StatusText);
        Assert.Equal(
            "Usando o banco de dados deste computador",
            viewModel.StatusDescription);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ServerStatusChangesWhenConnectionIsLostAndRestored()
    {
        var shouldFail = true;
        var settings = new BuildPcApiSettings
        {
            BaseUrl = "https://buildpc.example/api/",
            ApiKey = "secret"
        };
        var viewModel = new ConnectionStatusViewModel(
            settings,
            _ => shouldFail
                ? Task.FromException(new HttpRequestException("offline"))
                : Task.CompletedTask);

        Assert.True(viewModel.IsOnline);

        await viewModel.RefreshAsync();

        Assert.True(viewModel.IsOffline);
        Assert.Equal("OFFLINE", viewModel.StatusText);

        shouldFail = false;
        await viewModel.RefreshAsync();

        Assert.True(viewModel.IsOnline);
        Assert.Equal("ONLINE", viewModel.StatusText);
    }
}
