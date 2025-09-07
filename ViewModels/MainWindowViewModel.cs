using KamatekCrm.Commands; 
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace KamatekCrm.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        // Aktif olan ViewModel'i tutacak özellik
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

        // Diğer ViewModel'lerin örnekleri (instance)
        private readonly CustomersViewModel _customersViewModel;
        private readonly TicketsViewModel _ticketsViewModel;

        // Gezinti Komutları
        public ICommand ShowCustomersViewCommand { get; }
        public ICommand ShowTicketsViewCommand { get; }

        public MainWindowViewModel()
        {
            // ViewModelleri başlat
            _customersViewModel = new CustomersViewModel();
            _ticketsViewModel = new TicketsViewModel();

            // Komutları tanımla (RelayCommand kullanarak)
            ShowCustomersViewCommand = new RelayCommand(ShowCustomersView);
            ShowTicketsViewCommand = new RelayCommand(ShowTicketsView);

            // Uygulama açıldığında varsayılan olarak Müşteriler ekranını göster
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
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}