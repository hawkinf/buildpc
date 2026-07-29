using Avalonia.Threading;

namespace BuildPc.Desktop.Services;

/// <summary>
/// Adia uma ação até que a sequência de chamadas pare. Permite que os
/// ViewModels sejam testados sem o laço de mensagens do Avalonia.
/// </summary>
public interface IDebouncer
{
    /// <summary>
    /// Reinicia a contagem. A ação só corre quando o período configurado passa
    /// sem novas chamadas.
    /// </summary>
    void Trigger();

    void Cancel();
}

/// <summary>Cria o debouncer usado por um ViewModel.</summary>
public delegate IDebouncer DebouncerFactory(Action callback);

/// <summary>
/// Implementação de produção: espera o usuário parar de digitar antes de
/// reavaliar filtros que percorrem todo o catálogo.
/// </summary>
public sealed class DebounceTimer : IDebouncer
{
    /// <summary>
    /// Curto o bastante para não ser percebido como atraso, longo o
    /// suficiente para absorver a digitação normal de um filtro.
    /// </summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(220);

    private readonly DispatcherTimer _timer;

    public DebounceTimer(Action callback, TimeSpan? delay = null)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _timer = new DispatcherTimer
        {
            Interval = delay ?? DefaultDelay
        };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            callback();
        };
    }

    public void Trigger()
    {
        _timer.Stop();
        _timer.Start();
    }

    public void Cancel() => _timer.Stop();
}

/// <summary>
/// Executa a ação na hora. Usada nos testes, onde não existe dispatcher para
/// disparar o temporizador.
/// </summary>
public sealed class ImmediateDebouncer : IDebouncer
{
    private readonly Action _callback;

    public ImmediateDebouncer(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _callback = callback;
    }

    public void Trigger() => _callback();

    public void Cancel()
    {
    }
}
