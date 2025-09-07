using KamatekCrm.Commands; // RelayCommand sınıfınızın burada olduğunu varsayıyoruz
using KamatekCrm.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace KamatekCrm.ViewModels
{
    public class TicketsViewModel : INotifyPropertyChanged
    {
        private readonly ObservableCollection<Ticket> _tickets = [];
        private Ticket _newOrEditTicket = new();
        private Ticket? _selectedTicket; // Seçim için kullanılacak doğru yedekleme alanı
        private int _nextId = 1;
        // private Ticket? _SelectedTicket; // <-- Gereksiz ve hataya neden olan kopya alan kaldırıldı.

        public ObservableCollection<Ticket> Tickets => _tickets;

        public Ticket NewOrEditTicket
        {
            get => _newOrEditTicket;
            set
            {
                _newOrEditTicket = value;
                OnPropertyChanged();
            }
        }

        public Ticket? SelectedTicket
        {
            get => _selectedTicket;
            set
            {
                // --- KRİTİK DÜZELTME BAŞLANGICI ---
                // 'value' değişkenini büyük harfli _SelectedTicket yerine küçük harfli _selectedTicket alanına atıyoruz.
                _selectedTicket = value;
                // --- KRİTİK DÜZELTME SONU ---

                OnPropertyChanged();

                if (value != null)
                {
                    // Seçili öğenin verilerini düzenleme formuna kopyala
                    NewOrEditTicket = new Ticket { Id = value.Id, Title = value.Title, Description = value.Description };
                }
                else
                {
                    // Seçim kaldırıldığında formu temizle
                    NewOrEditTicket = new Ticket();
                }

                // Komutların CanExecute durumunu güncelle (Butonları etkinleştir/devre dışı bırak)
                if (UpdateCommand is RelayCommand updateRelay)
                    updateRelay.RaiseCanExecuteChanged();

                if (DeleteCommand is RelayCommand deleteRelay)
                    deleteRelay.RaiseCanExecuteChanged();
            }
        }

        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }

        public TicketsViewModel()
        {
            AddCommand = new RelayCommand(AddTicket);
            UpdateCommand = new RelayCommand(UpdateTicket, () => SelectedTicket?.Id > 0);
            DeleteCommand = new RelayCommand(DeleteTicket, () => SelectedTicket?.Id > 0);
        }

        private void AddTicket()
        {
            if (string.IsNullOrWhiteSpace(NewOrEditTicket.Title))
                return;

            var newTicket = new Ticket
            {
                Id = _nextId++, 
                Title = NewOrEditTicket.Title,
                Description = NewOrEditTicket.Description,
                CreatedDate = DateTime.Now
            };

            Tickets.Add(newTicket);
            NewOrEditTicket = new Ticket(); // Formu temizle
        }

        private void UpdateTicket()
        {
            if (SelectedTicket == null) return;

            SelectedTicket.Title = NewOrEditTicket.Title;
            SelectedTicket.Description = NewOrEditTicket.Description;

            SelectedTicket = null; // Seçimi ve formu temizle
        }

        private void DeleteTicket()
        {
            if (SelectedTicket == null) return;
            Tickets.Remove(SelectedTicket);
            SelectedTicket = null; // Seçimi temizle
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}