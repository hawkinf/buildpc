using System.Windows.Input;
using BuildPc.Desktop.Services;

namespace BuildPc.Desktop.ViewModels;

public sealed class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        !_isExecuting && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        NotifyCanExecuteChanged();
        try
        {
            await execute();
        }
        catch (Exception exception)
        {
            // Agora que toda leitura e gravação é assíncrona, este é o funil por
            // onde as falhas dos comandos passam. Sem o catch, um erro que o
            // ViewModel não tratou viraria exceção não observada.
            CrashLogService.Record("Comando", exception);
        }
        finally
        {
            _isExecuting = false;
            NotifyCanExecuteChanged();
        }
    }

    public void NotifyCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
