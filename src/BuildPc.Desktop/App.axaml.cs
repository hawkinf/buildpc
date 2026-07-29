using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            StartMainWindow(desktop, forceLocalDatabase: false);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void StartMainWindow(
        IClassicDesktopStyleApplicationLifetime desktop,
        bool forceLocalDatabase)
    {
        MainWindowViewModel viewModel;
        try
        {
            viewModel = new MainWindowViewModel(forceLocalDatabase);
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
                    StartMainWindow(desktop, forceLocalDatabase: true);
                    break;
                case StartupErrorChoice.Retry:
                    StartMainWindow(desktop, forceLocalDatabase: false);
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
