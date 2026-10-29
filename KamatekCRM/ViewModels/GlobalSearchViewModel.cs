using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Global arama ViewModel
    /// </summary>
    public partial class GlobalSearchViewModel : ViewModelBase
    {
        private string _searchQuery = string.Empty;
        private bool _isSearching;
        private bool _showResults;
        private SearchResult? _selectedResult;

        private System.Threading.CancellationTokenSource? _searchCts;

        /// <summary>
        /// Arama sorgusu
        /// </summary>
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    _searchCts?.Cancel();
                    _searchCts = new System.Threading.CancellationTokenSource();
                    var token = _searchCts.Token;
                    _ = DebouncedSearchAsync(token);
                }
            }
        }

        private async Task DebouncedSearchAsync(System.Threading.CancellationToken token)
        {
            try
            {
                await Task.Delay(300, token);
                if (!token.IsCancellationRequested)
                {
                    await PerformSearchAsync(token);
                }
            }
            catch (System.OperationCanceledException)
            {
                // Debounced keystroke was superseded
            }
        }

        /// <summary>
        /// Arama yapılıyor mu?
        /// </summary>
        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }

        /// <summary>
        /// Sonuçları göster
        /// </summary>
        public bool ShowResults
        {
            get => _showResults;
            set => SetProperty(ref _showResults, value);
        }

        /// <summary>
        /// Seçili sonuç
        /// </summary>
        public SearchResult? SelectedResult
        {
            get => _selectedResult;
            set
            {
                if (SetProperty(ref _selectedResult, value) && value != null)
                {
                    NavigateToResult(value);
                }
            }
        }

        /// <summary>
        /// Arama sonuçları
        /// </summary>
        public ObservableCollection<SearchResult> Results { get; } = new ObservableCollection<SearchResult>();

        /// <summary>
        /// Sonuç var mı?
        /// </summary>
        public bool HasResults => Results.Count > 0;

        /// <summary>
        /// Sonuç yok mesajı göster
        /// </summary>
        public bool ShowNoResults => !HasResults && !string.IsNullOrWhiteSpace(SearchQuery) && SearchQuery.Length >= 2;

        /// <summary>
        /// Sonuç seçme event'i
        /// </summary>
        public event System.Action<string, int>? ResultSelected;

        /// <summary>
        /// Constructor
        /// </summary>
        private readonly ISearchService _searchService;

        public GlobalSearchViewModel(ISearchService searchService)
        {
            _searchService = searchService;
        }

        private async Task PerformSearchAsync(System.Threading.CancellationToken token = default)
        {
            Results.Clear();

            if (string.IsNullOrWhiteSpace(SearchQuery) || SearchQuery.Length < 2)
            {
                ShowResults = false;
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(ShowNoResults));
                return;
            }

            IsSearching = true;
            ShowResults = true;

            try
            {
                var results = await _searchService.SearchAsync(SearchQuery, 30, token);
                foreach (var result in results)
                {
                    Results.Add(result);
                }
            }
            catch (System.OperationCanceledException)
            {
                // Request cancelled
            }
            finally
            {
                IsSearching = false;
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(ShowNoResults));
            }
        }

        /// <summary>
        /// Aramayı temizle
        /// </summary>
        [RelayCommand]
        private void Clear()
        {
            _searchCts?.Cancel();
            SearchQuery = string.Empty;
            Results.Clear();
            ShowResults = false;
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(ShowNoResults));
        }

        /// <summary>
        /// Listeden sonuç seçildiğinde çağrılır (tıklama / klavye).
        /// </summary>
        [RelayCommand]
        private void SelectResult(SearchResult? result)
        {
            if (result is null) return;

            // Aynı sonuç nesnesi tekrar seçilirse SetProperty false döner ve
            // navigasyon atlanır; bu durumda doğrudan yönlendiriyoruz.
            if (ReferenceEquals(_selectedResult, result))
            {
                NavigateToResult(result);
                return;
            }

            SelectedResult = result; // setter -> NavigateToResult
        }

        /// <summary>
        /// Sonuca git
        /// </summary>
        private void NavigateToResult(SearchResult result)
        {
            ResultSelected?.Invoke(result.EntityType, result.Id);
            Clear();
        }

        /// <summary>
        /// Sonuçları kapat
        /// </summary>
        public void CloseResults()
        {
            ShowResults = false;
        }
    }
}

