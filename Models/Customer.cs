
﻿using System.ComponentModel;

using System.ComponentModel;

using System.Runtime.CompilerServices; // Bu satırın eklendiğinden emin olun

namespace KamatekCrm.Models
{
    public class Customer : INotifyPropertyChanged
    {
        private int _id;
        private string _name = "";
        private string _surname = "";
        private string _email = "";
        private string _phone = "";

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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
