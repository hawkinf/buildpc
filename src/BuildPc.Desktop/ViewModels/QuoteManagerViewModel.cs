using System.Collections.ObjectModel;
using System.Windows.Input;
using BuildPc.Core.Models;
using BuildPc.Core.Services;
using Microsoft.Data.Sqlite;

namespace BuildPc.Desktop.ViewModels;

public sealed class QuoteManagerViewModel : ViewModelBase
{
    private readonly IQuoteRepository _repository;
    private readonly Action<SavedQuote>? _openInAssembly;
    private SavedQuoteListItemViewModel? _selectedQuote;
    private string _statusMessage = string.Empty;
    private bool _isDeleteConfirmationVisible;

    public QuoteManagerViewModel(
        IQuoteRepository repository,
        Action<SavedQuote>? openInAssembly = null)
    {
        _repository = repository;
        _openInAssembly = openInAssembly;
        Quotes = [];
        RequestDeleteCommand = new RelayCommand(RequestDelete);
        ConfirmDeleteCommand = new AsyncRelayCommand(ConfirmDeleteAsync);
        CancelDeleteCommand = new RelayCommand(CancelDelete);
        OpenInAssemblyCommand = new RelayCommand(OpenInAssembly);
    }

    public ObservableCollection<SavedQuoteListItemViewModel> Quotes { get; }
    public ICommand RequestDeleteCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }
    public ICommand OpenInAssemblyCommand { get; }

    public SavedQuoteListItemViewModel? SelectedQuote
    {
        get => _selectedQuote;
        set
        {
            if (SetProperty(ref _selectedQuote, value))
            {
                IsDeleteConfirmationVisible = false;
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => SelectedQuote is not null;
    public bool HasQuotes => Quotes.Count > 0;
    public bool IsEmpty => !HasQuotes;

    public bool IsDeleteConfirmationVisible
    {
        get => _isDeleteConfirmationVisible;
        private set => SetProperty(ref _isDeleteConfirmationVisible, value);
    }

    public string DeleteConfirmationMessage =>
        SelectedQuote is null
            ? string.Empty
            : $"Excluir o orçamento #{SelectedQuote.Quote.Number:000000} de " +
              $"{SelectedQuote.Quote.ClientName}? Esta ação não pode ser desfeita " +
              "e o número não será reaproveitado.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public async Task RefreshAsync()
    {
        IReadOnlyList<SavedQuote> quotes;
        try
        {
            quotes = await _repository.GetQuotesAsync();
        }
        catch (SqliteException)
        {
            StatusMessage = "Não foi possível ler os orçamentos do banco local.";
            return;
        }
        catch (InvalidOperationException)
        {
            StatusMessage =
                "Não foi possível ler os orçamentos no servidor. " +
                "A lista pode estar desatualizada.";
            return;
        }

        var selectedId = SelectedQuote?.Quote.Id;
        Quotes.Clear();
        foreach (var quote in quotes)
        {
            Quotes.Add(new SavedQuoteListItemViewModel(quote));
        }

        SelectedQuote = Quotes.FirstOrDefault(item => item.Quote.Id == selectedId) ??
                        Quotes.FirstOrDefault();
        OnPropertyChanged(nameof(HasQuotes));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void OpenInAssembly()
    {
        if (SelectedQuote is not { } selected)
        {
            return;
        }

        if (_openInAssembly is null)
        {
            StatusMessage = "A Montagem não está disponível para abrir orçamentos.";
            return;
        }

        IsDeleteConfirmationVisible = false;
        _openInAssembly(selected.Quote);
    }

    private void RequestDelete()
    {
        if (SelectedQuote is not null)
        {
            StatusMessage = string.Empty;
            IsDeleteConfirmationVisible = true;
            OnPropertyChanged(nameof(DeleteConfirmationMessage));
        }
    }

    private void CancelDelete() => IsDeleteConfirmationVisible = false;

    private async Task ConfirmDeleteAsync()
    {
        if (SelectedQuote is not { } selected)
        {
            IsDeleteConfirmationVisible = false;
            return;
        }

        var number = selected.Quote.Number;
        try
        {
            if (!await _repository.DeleteQuoteAsync(selected.Quote.Id))
            {
                StatusMessage = "O orçamento já não estava mais gravado.";
                IsDeleteConfirmationVisible = false;
                await RefreshAsync();
                return;
            }
        }
        catch (SqliteException)
        {
            StatusMessage = "Não foi possível excluir o orçamento do banco local.";
            IsDeleteConfirmationVisible = false;
            return;
        }
        catch (InvalidOperationException)
        {
            StatusMessage = "Não foi possível excluir o orçamento no servidor.";
            IsDeleteConfirmationVisible = false;
            return;
        }

        IsDeleteConfirmationVisible = false;
        SelectedQuote = null;
        await RefreshAsync();
        StatusMessage = $"Orçamento #{number:000000} excluído.";
    }

    public void CompletePdfPreview(bool opened)
    {
        StatusMessage = opened
            ? "PDF aberto. Use o visualizador para salvar ou imprimir."
            : "O PDF foi gerado, mas não foi possível abri-lo automaticamente.";
    }

    public void FailPdfPreview() =>
        StatusMessage = "Não foi possível gerar a visualização do PDF.";
}
