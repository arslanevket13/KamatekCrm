using System.ComponentModel;
using System.Runtime.CompilerServices;
using KamatekCrm.Models;

namespace KamatekCrm.Models
{
    public class Ticket : INotifyPropertyChanged
    {
        private int _id;
        private string _title = string.Empty;
        private string _description = string.Empty;
        private DateTime _createdDate;
        private int _customerId;
        private Customer? _customer;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public DateTime CreatedDate
        {
            get => _createdDate;
            set { _createdDate = value; OnPropertyChanged(); }
        }

        public int CustomerId
        {
            get => _customerId;
            set { _customerId = value; OnPropertyChanged(); }
        }

        public Customer? Customer 
        {
            get => _customer;
            set { _customer = value; OnPropertyChanged(); }
        }

        public Ticket(int id, string title, string description, DateTime createdDate, int customerId)
        {
            Id = id;
            Title = title;
            Description = description;
            CreatedDate = createdDate;
            CustomerId = customerId;
        }

        public Ticket() : this(0, string.Empty, string.Empty, DateTime.Now, 0)
        {
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}