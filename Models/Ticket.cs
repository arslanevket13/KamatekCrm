using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KamatekCrm.Models
{
    public class Ticket : INotifyPropertyChanged
    {
        private int _id;
        private string _title = string.Empty;
        private string _description = string.Empty;
        private DateTime _createdDate;

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

        public Ticket(int id, string title, string description, DateTime createdDate)
        {
            Id = id;
            Title = title;
            Description = description;
            CreatedDate = createdDate;
        }

        public Ticket() : this(0, string.Empty, string.Empty, DateTime.Now)
        {
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        // Düzeltilmiş kod:
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(propertyName);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}