
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KamatekCrm.Models
{
    public class Customer : INotifyPropertyChanged
    {
        private int _id;
        private string _name = "";
        private string _surname = "";
        private string _email = "";
        private string _phone = "";
        private string _adres = "";

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }
       

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Surname
        {
            get => _surname;
            set { _surname = value; OnPropertyChanged(); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string Phone
        {
            get => _phone;
            set { _phone = value; OnPropertyChanged(); }
        }

        public string Adres
        {
            get => _adres;
            set { _adres = value; OnPropertyChanged(); }
        }

        // 💡 YENİ: Parametreli Constructor
        public Customer(int id, string name, string surname, string email, string phone, string adres)
        {
            Id = id;
            Name = name;
            Surname = surname;
            Email = email;
            Phone = phone;
            Adres = adres;
        }

        // Varsayılan constructor
        public Customer() : this(0, "", "", "", "", "")
        {
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}