using KamatekCrm.ViewModels;
using System.Windows;

namespace KamatekCrm.Services
{
    public class LoadingService : ILoadingService
    {
        private readonly LoadingViewModel _loadingViewModel;

        public LoadingService(LoadingViewModel loadingViewModel)
        {
            _loadingViewModel = loadingViewModel;
        }

        public void Show(string message = "Yükleniyor...")
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _loadingViewModel.BusyMessage = message;
                _loadingViewModel.IsBusy = true;
            });
        }

        public void Hide()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _loadingViewModel.IsBusy = false;
            });
        }
    }
}
