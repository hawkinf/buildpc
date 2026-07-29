using System.Collections.ObjectModel;
using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Desktop.ViewModels;

public sealed class QuoteManagerViewModel : ViewModelBase
{
    private readonly QuoteRepository _repository;
    private SavedQuoteListItemViewModel? _selectedQuote;
    private string _statusMessage = string.Empty;

    public QuoteManagerViewModel(QuoteRepository repository)
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
        var selectedId = SelectedQuote?.Quote.Id;
        Quotes.Clear();
        foreach (var quote in _repository.GetQuotes())
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
