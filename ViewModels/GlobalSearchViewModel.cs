using System.Collections.ObjectModel;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Global arama ViewModel
    /// </summary>
    public class GlobalSearchViewModel : ViewModelBase
    {
        private string _searchQuery = string.Empty;
        private bool _isSearching;
        private bool _showResults;
        private SearchResult? _selectedResult;

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
                    PerformSearch();
                }
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
        /// Temizle komutu
        /// </summary>
        public ICommand ClearCommand { get; }

        /// <summary>
        /// Sonuç seçme event'i
        /// </summary>
        public event System.Action<string, int>? ResultSelected;

        /// <summary>
        /// Constructor
        /// </summary>
        public GlobalSearchViewModel()
        {
            ClearCommand = new RelayCommand(_ => ClearSearch());
        }

        /// <summary>
        /// Arama yap
        /// </summary>
        private void PerformSearch()
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
                var results = SearchService.Search(SearchQuery);
                foreach (var result in results)
                {
                    Results.Add(result);
                }
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
        private void ClearSearch()
        {
            SearchQuery = string.Empty;
            Results.Clear();
            ShowResults = false;
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(ShowNoResults));
        }

        /// <summary>
        /// Sonuca git
        /// </summary>
        private void NavigateToResult(SearchResult result)
        {
            ResultSelected?.Invoke(result.EntityType, result.Id);
            ClearSearch();
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
