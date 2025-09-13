using KamatekCrm.Commands;
using KamatekCrm.Models;
using KamatekCrm.Services;
using KamatekCrm.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace KamatekCrm.ViewModels
{
    public class TicketsViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<Ticket> _tickets = [];
        private Ticket _selectedTicket = new Ticket();
        private ObservableCollection<Customer> _allCustomers = new ObservableCollection<Customer>();
        private int _nextId = 1;

        public ObservableCollection<Ticket> Tickets
        {
            get => _tickets;
            set
            {
                _tickets = value;
                OnPropertyChanged();
            }
        }

        public Ticket SelectedTicket
        {
            get => _selectedTicket;
            set
            {
                _selectedTicket = value;
                OnPropertyChanged();
                (UpdateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // 💡 Tüm müşterileri tutar — ComboBox için
        public ObservableCollection<Customer> AllCustomers
        {
            get => _allCustomers;
            set
            {
                _allCustomers = value;
                OnPropertyChanged();
            }
        }

        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }

        public ICommand OpenAddCustomerCommand { get; }

        public TicketsViewModel()
        {
            Tickets = new ObservableCollection<Ticket>();
            SelectedTicket = new Ticket();

            // 💡 Artık örnek veri yok — CustomerService'ten al
            AllCustomers = CustomerService.Instance.Customers;

            AddCommand = new RelayCommand(AddTicket, CanAddTicket);
            UpdateCommand = new RelayCommand(UpdateTicket, CanModifyTicket);
            DeleteCommand = new RelayCommand(DeleteTicket, CanModifyTicket);
            OpenAddCustomerCommand = new RelayCommand(OpenAddCustomer);
            CustomerService.Instance.CustomerAdded += OnCustomerAdded;

        }



            private void OnCustomerAdded(Customer customer)
        {
            SelectedTicket.CustomerId = customer.Id; // ← Otomatik seç
        }


        private void OpenAddCustomer()
        {
            var customersViewModel = new CustomersViewModel(this);
            var customersView = new CustomersView
            {
                DataContext = customersViewModel
            };
            
        



            var window = new Window
            {
                Title = "Müşteri Ekle",
                Content = new CustomersView(), // ← UserControl burada kullanılıyor
                Width = 1000,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            window.Show();
        }


        private bool CanAddTicket() => !string.IsNullOrWhiteSpace(SelectedTicket.Title) && SelectedTicket.CustomerId > 0;

        private bool CanModifyTicket() => SelectedTicket != null && SelectedTicket.Id > 0;

        private void AddTicket()
        {
            if (!CanAddTicket()) return;

            var customer = AllCustomers.FirstOrDefault(c => c.Id == SelectedTicket.CustomerId);

            var newTicket = new Ticket
            {
                Id = _nextId++,
                Title = SelectedTicket.Title,
                Description = SelectedTicket.Description,
                CreatedDate = DateTime.Now,
                CustomerId = SelectedTicket.CustomerId,
                Customer = customer! // ← Artık uyarı vermez, çünkü Customer? tipinde
            };

            Tickets.Add(newTicket);

            SelectedTicket.Title = string.Empty;
            SelectedTicket.Description = string.Empty;
            SelectedTicket.CustomerId = 0;
        }

    

            // Formu temizle
        
   

        private void UpdateTicket()
        {
            if (!CanModifyTicket()) return;

            // Zaten SelectedTicket üzerinden düzenleme yapılıyor
            // Referans tip olduğu için doğrudan koleksiyondaki nesne güncellenir
        }

        private void DeleteTicket()
        {
            if (!CanModifyTicket()) return;

            Tickets.Remove(SelectedTicket);
            SelectedTicket = new Ticket();
        }

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}