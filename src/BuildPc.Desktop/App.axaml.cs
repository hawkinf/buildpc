using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using BuildPc.Desktop.Services;
using BuildPc.Desktop.ViewModels;
using BuildPc.Desktop.Views;

namespace BuildPc.Desktop;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Enquanto a recuperação estiver em andamento não existe janela
            // principal; fechar o aviso não pode encerrar o processo sozinho.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // A carga do catálogo é assíncrona. Sem o Post, aguardá-la aqui
            // bloquearia a inicialização do próprio laço de mensagens.
            Dispatcher.UIThread.Post(
                () => _ = StartMainWindowAsync(desktop, forceLocalDatabase: false));
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task StartMainWindowAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        bool forceLocalDatabase)
    {
        MainWindowViewModel viewModel;
        try
        {
            viewModel = await MainWindowViewModel
                .CreateAsync(forceLocalDatabase)
                .ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            CrashLogService.Record("Inicialização", exception);
            ShowStartupError(desktop, exception);
            return;
        }

        var window = new MainWindow
        {
            DataContext = viewModel
        };
        desktop.MainWindow = window;
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
    }

    private static void ShowStartupError(
        IClassicDesktopStyleApplicationLifetime desktop,
        Exception exception)
    {
        var errorWindow = new StartupErrorWindow(DescribeFailure(exception));
        errorWindow.Closed += (_, _) =>
        {
            switch (errorWindow.Choice)
            {
                case StartupErrorChoice.UseLocalDatabase:
                    _ = StartMainWindowAsync(desktop, forceLocalDatabase: true);
                    break;
                case StartupErrorChoice.Retry:
                    _ = StartMainWindowAsync(desktop, forceLocalDatabase: false);
                    break;
                default:
                    desktop.Shutdown();
                    break;
            }
        };
        errorWindow.Show();
    }

    private static string DescribeFailure(Exception exception) =>
        exception is InvalidOperationException &&
        !string.IsNullOrWhiteSpace(exception.Message)
            ? exception.Message
            : "Não foi possível preparar os dados do BuildPC neste início. " +
              $"Os detalhes técnicos foram gravados em {CrashLogService.LogPath}.";
}
