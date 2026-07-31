using System.Collections.ObjectModel;
using System.Windows.Input;
using BuildPc.Core.Models;
using BuildPc.Core.Services;
using BuildPc.Desktop.Services;
using Microsoft.Data.Sqlite;

namespace BuildPc.Desktop.ViewModels;

public sealed class QuoteManagerViewModel : ViewModelBase
{
    private readonly IQuoteRepository _repository;
    private readonly Action<SavedQuote>? _openInAssembly;
    private readonly Action<SavedQuote>? _duplicateInAssembly;
    private readonly IDebouncer _searchDebounce;

    /// <summary>Todos os orçamentos lidos, antes de filtro e período.</summary>
    private readonly List<SavedQuoteListItemViewModel> _allQuotes = [];

    private SavedQuoteListItemViewModel? _selectedQuote;
    private string _statusMessage = string.Empty;
    private bool _isDeleteConfirmationVisible;
    private string _searchText = string.Empty;
    private QuotePeriodOptionViewModel _selectedPeriod = null!;
    private bool _includeDescriptionsInPdf = true;

    public QuoteManagerViewModel(
        IQuoteRepository repository,
        Action<SavedQuote>? openInAssembly = null,
        Action<SavedQuote>? duplicateInAssembly = null,
        DebouncerFactory? searchDebounceFactory = null)
    {
        _repository = repository;
        _openInAssembly = openInAssembly;
        _duplicateInAssembly = duplicateInAssembly;
        _searchDebounce =
            (searchDebounceFactory ?? (action => new DebounceTimer(action)))(
                ApplyFilter);
        Quotes = [];
        Periods =
        [
            new(QuotePeriod.All),
            new(QuotePeriod.Last7Days),
            new(QuotePeriod.Last30Days),
            new(QuotePeriod.Last90Days),
            new(QuotePeriod.ThisYear)
        ];
        _selectedPeriod = Periods[0];
        RequestDeleteCommand = new RelayCommand(RequestDelete);
        ConfirmDeleteCommand = new AsyncRelayCommand(ConfirmDeleteAsync);
        CancelDeleteCommand = new RelayCommand(CancelDelete);
        OpenInAssemblyCommand = new RelayCommand(OpenInAssembly);
        DuplicateCommand = new RelayCommand(DuplicateInAssembly);
        ClearSearchCommand = new RelayCommand(ClearSearch);
    }

    public ObservableCollection<SavedQuoteListItemViewModel> Quotes { get; }
    public IReadOnlyList<QuotePeriodOptionViewModel> Periods { get; }
    public ICommand RequestDeleteCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }
    public ICommand OpenInAssemblyCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasSearch));
                _searchDebounce.Trigger();
            }
        }
    }

    public bool HasSearch => !string.IsNullOrWhiteSpace(SearchText);

    public QuotePeriodOptionViewModel SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (value is not null && SetProperty(ref _selectedPeriod, value))
            {
                ApplyFilter();
            }
        }
    }

    public string CountText =>
        _allQuotes.Count == Quotes.Count
            ? Quotes.Count == 1 ? "1 orçamento" : $"{Quotes.Count} orçamentos"
            : $"{Quotes.Count} de {_allQuotes.Count} orçamentos";

    public bool IsFiltered => Quotes.Count != _allQuotes.Count;

    /// <summary>Soma vendida no recorte visível, útil para fechamento de período.</summary>
    public string FilteredTotalText =>
        Quotes
            .Sum(item => item.Quote.FinalPrice)
            .ToString("C", MainWindowViewModel.BrazilianCulture);

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

    public bool IncludeDescriptionsInPdf
    {
        get => _includeDescriptionsInPdf;
        set => SetProperty(ref _includeDescriptionsInPdf, value);
    }

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
        _allQuotes.Clear();
        _allQuotes.AddRange(quotes.Select(quote => new SavedQuoteListItemViewModel(quote)));
        ApplyFilter(selectedId);
    }

    /// <summary>
    /// Recorta a lista pela busca e pelo período em uma única passagem, e
    /// preserva a seleção quando o orçamento continua visível.
    /// </summary>
    private void ApplyFilter() => ApplyFilter(SelectedQuote?.Quote.Id);

    private void ApplyFilter(Guid? selectedId)
    {
        var now = DateTimeOffset.Now;
        var period = SelectedPeriod.Period;
        var visible = _allQuotes
            .Where(item =>
                QuoteFilter.IsInPeriod(item.Quote, period, now) &&
                QuoteFilter.Matches(item.Quote, SearchText))
            .ToList();

        Quotes.Clear();
        foreach (var item in visible)
        {
            Quotes.Add(item);
        }

        SelectedQuote = Quotes.FirstOrDefault(item => item.Quote.Id == selectedId) ??
                        Quotes.FirstOrDefault();
        OnPropertyChanged(nameof(HasQuotes));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsFiltered));
        OnPropertyChanged(nameof(FilteredTotalText));
    }

    private void ClearSearch() => SearchText = string.Empty;

    /// <summary>
    /// Abre uma copia do orçamento na Montagem para gravar como novo, mantendo
    /// o original intacto.
    /// </summary>
    private void DuplicateInAssembly()
    {
        if (SelectedQuote is not { } selected)
        {
            return;
        }

        if (_duplicateInAssembly is null)
        {
            StatusMessage = "A Montagem não está disponível para duplicar orçamentos.";
            return;
        }

        IsDeleteConfirmationVisible = false;
        _duplicateInAssembly(selected.Quote);
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
