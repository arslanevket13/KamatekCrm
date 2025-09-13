using KamatekCrm.Commands;
using KamatekCrm.Models;
using KamatekCrm.Services;
using KamatekCrm.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace KamatekCrm.ViewModels
{
    public class CustomersViewModel : INotifyPropertyChanged
    {
        private readonly TicketsViewModel? _ticketsViewModel;
        private Customer _selectedCustomer = new Customer();

        public ObservableCollection<Customer> Customers { get; }

        public Customer SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                _selectedCustomer = value;
                OnPropertyChanged();
                (AddCommand as RelayCommand)?.RaiseCanExecuteChanged(); // ← EKLENDİ
                (UpdateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }

        public ICommand CancelCommand { get; }

        public CustomersViewModel(TicketsViewModel ticketsViewModel)
        {
            _ticketsViewModel = ticketsViewModel;
            Customers = CustomerService.Instance.Customers;

            AddCommand = new RelayCommand(AddCustomer, CanAddCustomer);
            UpdateCommand = new RelayCommand(UpdateCustomer, CanModifyCustomer);
            DeleteCommand = new RelayCommand(DeleteCustomer, CanModifyCustomer);
            CancelCommand = new RelayCommand(CancelEdit);

            if (Customers.Any())
                SelectedCustomer = Customers.First();
        }

        private bool CanAddCustomer()
        {
            return !string.IsNullOrWhiteSpace(SelectedCustomer?.Name?.Trim()) &&
                   !string.IsNullOrWhiteSpace(SelectedCustomer?.Surname?.Trim());
        }

        private bool CanModifyCustomer()
        {
            return SelectedCustomer != null && SelectedCustomer.Id > 0;
        }

        private void AddCustomer()
        {
            System.Diagnostics.Debug.WriteLine($"Name: '{SelectedCustomer.Name}', Surname: '{SelectedCustomer.Surname}'");
            if (!CanAddCustomer())
            {
                System.Diagnostics.Debug.WriteLine("CanAddCustomer() returned false");
                return;
            }

            var newCustomer = new Customer
            {
                Name = SelectedCustomer.Name,
                Surname = SelectedCustomer.Surname,
                Email = SelectedCustomer.Email,
                Phone = SelectedCustomer.Phone,
                Adres = SelectedCustomer.Adres
            };

            CustomerService.Instance.AddCustomer(newCustomer);
            SelectedCustomer = new Customer();
        }

        private static void OpenAddCustomer()
        {
            
           
        }
        private void UpdateCustomer()
        {
            if (!CanModifyCustomer()) return;

            CustomerService.Instance.UpdateCustomer(SelectedCustomer);
        }

        private void DeleteCustomer()
        {
            if (!CanModifyCustomer()) return;

            CustomerService.Instance.DeleteCustomer(SelectedCustomer.Id);
            SelectedCustomer = new Customer();
        }

        private void CancelEdit()
        {
            SelectedCustomer = new Customer(); // Formu temizle
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}