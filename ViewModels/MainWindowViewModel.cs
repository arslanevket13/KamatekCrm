using KamatekCrm.Commands;
using KamatekCrm.ViewModels; // ← ViewModel'leri kullanmak için
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace KamatekCrm.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private object _currentViewModel = null!;
        public object CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                _currentViewModel = value;
                OnPropertyChanged();
            }
        }

        private readonly CustomersViewModel _customersViewModel;
        private readonly TicketsViewModel _ticketsViewModel;

        public ICommand ShowCustomersViewCommand { get; }
        public ICommand ShowTicketsViewCommand { get; }

        public MainWindowViewModel()
        {
            // 1. Önce TicketsViewModel oluştur
            _ticketsViewModel = new TicketsViewModel();

            // 2. Sonra CustomersViewModel'e aynı örneği geç
            _customersViewModel = new CustomersViewModel(_ticketsViewModel);

            // 3. Komutları tanımla
            ShowCustomersViewCommand = new RelayCommand(ShowCustomersView);
            ShowTicketsViewCommand = new RelayCommand(ShowTicketsView);

            // 4. Varsayılan görünüm
            CurrentViewModel = _customersViewModel;
        }

        private void ShowCustomersView()
        {
            CurrentViewModel = _customersViewModel;
        }

        private void ShowTicketsView()
        {
            CurrentViewModel = _ticketsViewModel;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}