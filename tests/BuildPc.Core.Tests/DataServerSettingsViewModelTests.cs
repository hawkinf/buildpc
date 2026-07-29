using BuildPc.Core.Services;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Core.Tests;

public sealed class DataServerSettingsViewModelTests
{
    [Fact]
    public void SaveServerNormalizesUrlAndRequestsRestart()
    {
        BuildPcApiSettings? saved = null;
        var viewModel = new DataServerSettingsViewModel(
            settings: null,
            settings => saved = settings,
            _ => Task.CompletedTask)
        {
            UseServer = true,
            ServerUrl = "https://nova-vps.example/buildpc-api",
            ApiKey = "secret"
        };

        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(saved);
        Assert.Equal(
            "https://nova-vps.example/buildpc-api/",
            saved.BaseUrl);
        Assert.Equal("secret", saved.ApiKey);
        Assert.True(viewModel.IsSuccess);
        Assert.Contains("Reinicie", viewModel.StatusMessage);
    }

    [Fact]
    public void SaveLocalModeDisablesRemoteServer()
    {
        var initial = new BuildPcApiSettings
        {
            BaseUrl = "https://servidor.example/buildpc-api/",
            ApiKey = "secret"
        };
        var savedRemoteValue = initial;
        var viewModel = new DataServerSettingsViewModel(
            initial,
            settings => savedRemoteValue = settings,
            _ => Task.CompletedTask)
        {
            UseServer = false
        };

        viewModel.SaveCommand.Execute(null);

        Assert.Null(savedRemoteValue);
        Assert.True(viewModel.IsSuccess);
        Assert.Contains("Banco local", viewModel.StatusMessage);
    }

    [Fact]
    public async Task TestConnectionUsesCurrentFormValues()
    {
        BuildPcApiSettings? tested = null;
        var viewModel = new DataServerSettingsViewModel(
            settings: null,
            _ => { },
            settings =>
            {
                tested = settings;
                return Task.CompletedTask;
            })
        {
            UseServer = true,
            ServerUrl = "https://nova-vps.example/api",
            ApiKey = "secret"
        };

        await viewModel.TestConnectionAsync();

        Assert.NotNull(tested);
        Assert.Equal("https://nova-vps.example/api/", tested.BaseUrl);
        Assert.True(viewModel.IsSuccess);
        Assert.Equal("Conexão realizada com sucesso.", viewModel.StatusMessage);
    }
}
