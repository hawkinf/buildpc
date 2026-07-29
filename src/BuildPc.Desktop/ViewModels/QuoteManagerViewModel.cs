using System.Collections.ObjectModel;
using BuildPc.Core.Models;
using BuildPc.Core.Services;
using Microsoft.Data.Sqlite;

namespace BuildPc.Desktop.ViewModels;

public sealed class QuoteManagerViewModel : ViewModelBase
{
    private readonly IQuoteRepository _repository;
    private SavedQuoteListItemViewModel? _selectedQuote;
    private string _statusMessage = string.Empty;

    public QuoteManagerViewModel(IQuoteRepository repository)
    {
        _repository = repository;
        Quotes = [];
        Refresh();
    }

    public ObservableCollection<SavedQuoteListItemViewModel> Quotes { get; }

    public SavedQuoteListItemViewModel? SelectedQuote
    {
        get => _selectedQuote;
        set
        {
            if (SetProperty(ref _selectedQuote, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => SelectedQuote is not null;
    public bool HasQuotes => Quotes.Count > 0;
    public bool IsEmpty => !HasQuotes;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public void Refresh()
    {
        IReadOnlyList<SavedQuote> quotes;
        try
        {
            quotes = _repository.GetQuotes();
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

    public void CompletePdfPreview(bool opened)
    {
        StatusMessage = opened
            ? "PDF aberto. Use o visualizador para salvar ou imprimir."
            : "O PDF foi gerado, mas não foi possível abri-lo automaticamente.";
    }

    public void FailPdfPreview() =>
        StatusMessage = "Não foi possível gerar a visualização do PDF.";
}
