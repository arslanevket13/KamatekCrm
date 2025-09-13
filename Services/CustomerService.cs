using System.Collections.ObjectModel;
using KamatekCrm.Models;

namespace KamatekCrm.Services
{
    public class CustomerService
    {
        private static readonly CustomerService _instance = new();
        public static CustomerService Instance => _instance;

        public ObservableCollection<Customer> Customers { get; } = new ObservableCollection<Customer>();

        private int _nextId = 1;

        private CustomerService()
        {
            // Örnek müşteriler (isteğe bağlı)
            Customers.Add(new Customer(_nextId++, "Ayşe", "Yılmaz", "ayse@mail.com", "5551234567", "Ankara Çankaya"));
            Customers.Add(new Customer(_nextId++, "Mehmet", "Kaya", "mehmet@mail.com", "5559876543", "İstanbul Kadıköy"));
        }
        public event Action<Customer>? CustomerAdded;
        public void AddCustomer(Customer customer)
        {
            customer.Id = _nextId++;
            Customers.Add(customer);
            CustomerAdded?.Invoke(customer);
        }

        public void UpdateCustomer(Customer customer)
        {
            var existing = Customers.FirstOrDefault(c => c.Id == customer.Id);
            if (existing != null)
            {
                existing.Name = customer.Name;
                existing.Surname = customer.Surname;
                existing.Email = customer.Email;
                existing.Phone = customer.Phone;
                existing.Adres = customer.Adres;
            }
        }

        public void DeleteCustomer(int customerId)
        {
            var customer = Customers.FirstOrDefault(c => c.Id == customerId);
            if (customer != null)
                Customers.Remove(customer);
        }
    }
}