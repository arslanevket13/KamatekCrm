using KamatekCrm.Commands;
using KamatekCrm.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace KamatekCrm.ViewModels;

public class CustomersViewModel : INotifyPropertyChanged
{
    private ObservableCollection<Customer> _customers = [];
    private Customer _newOrEditCustomer = new();
    private Customer? _selectedCustomer; // ✅ ? eklendi

    public ObservableCollection<Customer> Customers
    {
        get => _customers;
        set { _customers = value; OnPropertyChanged(); }
    }

    public Customer NewOrEditCustomer
    {
        get => _newOrEditCustomer;
        set { _newOrEditCustomer = value; OnPropertyChanged(); }
    }

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (_selectedCustomer != value)
            {
                _selectedCustomer = value;
                OnPropertyChanged();

                if (value != null)
                {
                    NewOrEditCustomer = new Customer
                    {
                        Id = value.Id,
                        Name = value.Name,
                        Surname = value.Surname,
                        Email = value.Email,
                        Phone = value.Phone
                    };
                }
                else
                {
                    NewOrEditCustomer = new Customer();
                }

                ((RelayCommand)UpdateCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand AddCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CancelCommand { get; }

    public CustomersViewModel()
    {
        AddCommand = new RelayCommand(AddCustomer);
        UpdateCommand = new RelayCommand(UpdateCustomer, CanUpdateOrDelete);
        DeleteCommand = new RelayCommand(DeleteCustomer, CanUpdateOrDelete);
        CancelCommand = new RelayCommand(CancelEdit);

        // Örnek müşteriler
        Customers.Add(new Customer { Id = 1, Name = "Ali", Surname = "Veli", Email = "ali@veli.com", Phone = "555 123 45 67" });
        Customers.Add(new Customer { Id = 2, Name = "Ayşe", Surname = "Fatma", Email = "ayse@fatma.com", Phone = "555 987 65 43" });
    }

    private void AddCustomer()
    {
        if (string.IsNullOrWhiteSpace(NewOrEditCustomer.Name) ||
            string.IsNullOrWhiteSpace(NewOrEditCustomer.Surname))
            return;

        NewOrEditCustomer.Id = Customers.Any() ? Customers.Max(c => c.Id) + 1 : 1;
        Customers.Add(NewOrEditCustomer);
        NewOrEditCustomer = new Customer();
    }

    private void UpdateCustomer()
    {
        if (SelectedCustomer == null) return;

        SelectedCustomer.Name = NewOrEditCustomer.Name;
        SelectedCustomer.Surname = NewOrEditCustomer.Surname;
        SelectedCustomer.Email = NewOrEditCustomer.Email;
        SelectedCustomer.Phone = NewOrEditCustomer.Phone;

        SelectedCustomer = null;
    }

    private void DeleteCustomer()
    {
        if (SelectedCustomer == null) return;
        Customers.Remove(SelectedCustomer);
        SelectedCustomer = null;
    }

    private void CancelEdit()
    {
        SelectedCustomer = null;
        NewOrEditCustomer = new Customer();
    }

    private bool CanUpdateOrDelete() => SelectedCustomer != null;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}