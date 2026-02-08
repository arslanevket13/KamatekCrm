using System;
using System.Threading.Tasks;
using System.Windows.Input;
using KamatekCrm.Commands;

namespace KamatekCrm.ViewModels.Common
{
    public abstract class PaginationViewModel : ViewModelBase
    {
        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalCount;
        private int _totalPages;

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                // If page changes, refresh data unless explicitly suppressed or if change is programmatic without refresh intent
                // But for now, let's assume UI binding change means we want refresh.
                // However, setting it programmatically might trigger unwanted refresh.
                // Let's protect it or just call refresh.
                if (SetProperty(ref _currentPage, value))
                {
                     OnPropertyChanged(nameof(HasNextPage));
                     OnPropertyChanged(nameof(HasPreviousPage));
                    _ = RefreshDataAsync();
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
                    // If page size changes, reset to page 1 and refresh
                    if (_currentPage != 1) 
                        CurrentPage = 1; // Setter will trigger refresh
                    else
                        _ = RefreshDataAsync(); // Already at page 1, force refresh
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
                    TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);
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

        public ICommand NextPageCommand { get; }
        public ICommand PreviousPageCommand { get; }

        protected PaginationViewModel()
        {
            NextPageCommand = new RelayCommand(_ => NextPage(), _ => HasNextPage);
            PreviousPageCommand = new RelayCommand(_ => PreviousPage(), _ => HasPreviousPage);
        }

        private void NextPage()
        {
            if (HasNextPage) CurrentPage++;
        }

        private void PreviousPage()
        {
            if (HasPreviousPage) CurrentPage--;
        }

        protected abstract Task RefreshDataAsync();
    }
}
