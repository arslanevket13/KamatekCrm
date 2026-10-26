using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace KamatekCrm.ViewModels.Common
{
    public abstract partial class PaginationViewModel : ViewModelBase
    {
        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalCount;
        private int _totalPages;
        private bool _isRefreshing;
        private bool _suppressRefresh;

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    OnPropertyChanged(nameof(HasNextPage));
                    OnPropertyChanged(nameof(HasPreviousPage));
                    if (!_suppressRefresh)
                    {
                        _ = SafeRefreshDataAsync();
                    }
                }
            }
        }

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (SetProperty(ref _pageSize, value))
                {
                    // If page size changes, reset to page 1 and refresh without double refresh
                    if (_currentPage != 1)
                    {
                        CurrentPage = 1; // Setter handles refresh
                    }
                    else
                    {
                        _ = SafeRefreshDataAsync();
                    }
                }
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            set
            {
                if (SetProperty(ref _totalCount, value))
                {
                    TotalPages = (int)Math.Ceiling(Math.Max(0, TotalCount) / (double)Math.Max(1, PageSize));
                }
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                if (SetProperty(ref _totalPages, value))
                {
                    OnPropertyChanged(nameof(HasNextPage));
                    OnPropertyChanged(nameof(HasPreviousPage));
                }
            }
        }

        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        protected PaginationViewModel()
        {
        }

        [RelayCommand]
        protected void NextPage()
        {
            if (HasNextPage) CurrentPage++;
        }

        [RelayCommand]
        protected void PreviousPage()
        {
            if (HasPreviousPage) CurrentPage--;
        }

        /// <summary>
        /// Sayfa sıfırlanırken otomatik tetiklenmeyi bastırarak geçiş yapar
        /// </summary>
        public void ResetPageWithoutRefresh()
        {
            _suppressRefresh = true;
            try
            {
                CurrentPage = 1;
            }
            finally
            {
                _suppressRefresh = false;
            }
        }

        /// <summary>
        /// Eşzamanlı çift refresh çağrılarını önler
        /// </summary>
        protected async Task SafeRefreshDataAsync()
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            try
            {
                await RefreshDataAsync();
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        /// <summary>
        /// Bellek içi koleksiyonlar için sayfalama
        /// </summary>
        protected IEnumerable<T> ApplyPaging<T>(IEnumerable<T> source)
        {
            return source
                .Skip(Math.Max(0, (CurrentPage - 1) * PageSize))
                .Take(Math.Max(1, PageSize));
        }

        protected abstract Task RefreshDataAsync();
    }
}


