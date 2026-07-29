using Avalonia;
using BuildPc.Desktop.Services;

namespace BuildPc.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            CrashLogService.Record(
                "Exceção não tratada",
                eventArgs.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            CrashLogService.Record("Tarefa em segundo plano", eventArgs.Exception);
            eventArgs.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            CrashLogService.Record("Encerramento inesperado", exception);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
