using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BuildPc.Desktop.Views;

public enum StartupErrorChoice
{
    /// <summary>Janela fechada no “X”: o usuário quer sair do programa.</summary>
    Quit,
    Retry,
    UseLocalDatabase
}

public sealed partial class StartupErrorWindow : Window
{
    public StartupErrorWindow()
        : this("O servidor de dados não respondeu.")
    {
    }

    public StartupErrorWindow(string detail)
    {
        AvaloniaXamlLoader.Load(this);
        var text = this.FindControl<TextBlock>("detailText");
        if (text is not null)
        {
            text.Text = detail;
        }
    }

    public StartupErrorChoice Choice { get; private set; } = StartupErrorChoice.Quit;

    private void UseLocalDatabase_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        Choice = StartupErrorChoice.UseLocalDatabase;
        Close();
    }

    private void Retry_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        Choice = StartupErrorChoice.Retry;
        Close();
    }
}
