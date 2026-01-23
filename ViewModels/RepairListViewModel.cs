using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Cihaz Kabul & Tamir Listesi ViewModel
    /// </summary>
    public class RepairListViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private string _searchText = string.Empty;
        private RepairStatus? _selectedStatus;
        private DateTime? _startDate;
        private DateTime? _endDate;

        public ObservableCollection<RepairJobDisplayItem> AllRepairJobs { get; set; }
        public ICollectionView FilteredRepairJobs { get; private set; }

        public ObservableCollection<RepairStatusOption> StatusOptions { get; set; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    FilteredRepairJobs?.Refresh();
            }
        }

        public RepairStatus? SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetProperty(ref _selectedStatus, value))
                    FilteredRepairJobs?.Refresh();
            }
        }

        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                    FilteredRepairJobs?.Refresh();
            }
        }

        public DateTime? EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                    FilteredRepairJobs?.Refresh();
            }
        }

        public int TotalCount => FilteredRepairJobs?.Cast<object>().Count() ?? 0;

        // Commands
        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand NotifyCustomerCommand { get; }
        public ICommand ShowRepairPhotosCommand { get; }
        public ICommand PrintTicketCommand { get; }
        public ICommand OpenRepairDetailCommand { get; }

        public RepairListViewModel()
        {
            _context = new AppDbContext();
            AllRepairJobs = new ObservableCollection<RepairJobDisplayItem>();
            StatusOptions = new ObservableCollection<RepairStatusOption>();

            // Status seçeneklerini doldur
            InitializeStatusOptions();

            // Commands
            RefreshCommand = new RelayCommand(_ => LoadRepairJobs());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            NotifyCustomerCommand = new RelayCommand(ExecuteNotifyCustomer);
            ShowRepairPhotosCommand = new RelayCommand(ExecuteShowPhotos);
            PrintTicketCommand = new RelayCommand(ExecutePrintTicket);
            OpenRepairDetailCommand = new RelayCommand(ExecuteOpenDetail);

            // CollectionView
            FilteredRepairJobs = CollectionViewSource.GetDefaultView(AllRepairJobs);
            FilteredRepairJobs.Filter = FilterRepairJobs;
            FilteredRepairJobs.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalCount));

            // Veriyi yükle
            LoadRepairJobs();
        }

        private void InitializeStatusOptions()
        {
            StatusOptions.Add(new RepairStatusOption { Status = null, DisplayName = "Tümü", Icon = "📋" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.Registered, DisplayName = "Kayıt Açıldı", Icon = "📝" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.Diagnosing, DisplayName = "Arıza Tespiti", Icon = "🔍" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.WaitingForParts, DisplayName = "Parça Bekleniyor", Icon = "📦" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.SentToFactory, DisplayName = "Fabrikada", Icon = "🏭" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.InRepair, DisplayName = "Tamir Sürüyor", Icon = "🔧" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.Testing, DisplayName = "Test Aşaması", Icon = "✔️" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.ReadyForPickup, DisplayName = "Teslimata Hazır", Icon = "✅" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.Delivered, DisplayName = "Teslim Edildi", Icon = "🚗" });
        }

        private void LoadRepairJobs()
        {
            AllRepairJobs.Clear();

            try
            {
                var repairJobs = _context.ServiceJobs
                    .Include(j => j.Customer)
                    .Where(j => j.WorkOrderType == WorkOrderType.Repair)
                    .OrderByDescending(j => j.CreatedDate)
                    .ToList();

                foreach (var job in repairJobs)
                {
                    AllRepairJobs.Add(new RepairJobDisplayItem
                    {
                        Id = job.Id,
                        TicketNo = $"#TK-{job.Id:D4}",
                        CustomerName = job.Customer?.FullName ?? "Bilinmiyor",
                        CustomerPhone = job.Customer?.PhoneNumber ?? "",
                        DeviceBrand = job.DeviceBrand ?? "",
                        DeviceModel = job.DeviceModel ?? "",
                        SerialNumber = job.SerialNumber ?? "",
                        RepairStatus = job.RepairStatus,
                        CreatedDate = job.CreatedDate,
                        Price = job.Price,
                        Description = job.Description,
                        PhotoPathsJson = job.PhotoPathsJson
                    });
                }

                OnPropertyChanged(nameof(TotalCount));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veriler yüklenirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool FilterRepairJobs(object obj)
        {
            if (obj is not RepairJobDisplayItem job)
                return false;

            // Status filtresi
            if (SelectedStatus.HasValue && job.RepairStatus != SelectedStatus.Value)
                return false;

            // Tarih filtresi
            if (StartDate.HasValue && job.CreatedDate.Date < StartDate.Value.Date)
                return false;

            if (EndDate.HasValue && job.CreatedDate.Date > EndDate.Value.Date)
                return false;

            // Metin araması (Seri no, Model, Marka, Müşteri)
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLower();
                return job.SerialNumber.ToLower().Contains(search)
                    || job.DeviceModel.ToLower().Contains(search)
                    || job.DeviceBrand.ToLower().Contains(search)
                    || job.CustomerName.ToLower().Contains(search)
                    || job.TicketNo.ToLower().Contains(search);
            }

            return true;
        }

        private void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedStatus = null;
            StartDate = null;
            EndDate = null;
        }

        private void ExecuteNotifyCustomer(object? parameter)
        {
            if (parameter is RepairJobDisplayItem job)
            {
                MessageBox.Show(
                    $"Müşteri: {job.CustomerName}\nTelefon: {job.CustomerPhone}\n\nSMS/WhatsApp gönderme özelliği yakında eklenecek.",
                    "Müşteri Bilgilendirme",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void ExecuteShowPhotos(object? parameter)
        {
            if (parameter is RepairJobDisplayItem job)
            {
                if (string.IsNullOrEmpty(job.PhotoPathsJson))
                {
                    MessageBox.Show("Bu cihaza ait fotoğraf bulunmuyor.", "Fotoğraflar", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Fotoğraf görüntüleme özelliği yakında eklenecek.\n\nKayıtlı fotoğraf verisi: {job.PhotoPathsJson}", "Fotoğraflar", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void ExecutePrintTicket(object? parameter)
        {
            if (parameter is RepairJobDisplayItem job)
            {
                MessageBox.Show(
                    $"Servis Fişi\n\nTicket: {job.TicketNo}\nMüşteri: {job.CustomerName}\nCihaz: {job.DeviceBrand} {job.DeviceModel}\nSeri No: {job.SerialNumber}\n\nYazdırma özelliği yakında eklenecek.",
                    "Servis Fişi Yazdır",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void ExecuteOpenDetail(object? parameter)
        {
            if (parameter is RepairJobDisplayItem job)
            {
                // RepairTrackingWindow ile açılabilir
                MessageBox.Show($"Tamir detay penceresi: {job.TicketNo}", "Detay", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    /// <summary>
    /// Tamir işi görüntüleme modeli
    /// </summary>
    public class RepairJobDisplayItem
    {
        public int Id { get; set; }
        public string TicketNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string DeviceBrand { get; set; } = string.Empty;
        public string DeviceModel { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public RepairStatus RepairStatus { get; set; }
        public DateTime CreatedDate { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? PhotoPathsJson { get; set; }

        // Computed
        public string DeviceDisplay => $"{DeviceBrand} {DeviceModel}".Trim();
        public int DaysInShop => (DateTime.Now - CreatedDate).Days;
        
        public string StatusDisplay => RepairStatus switch
        {
            RepairStatus.Registered => "Kayıt Açıldı",
            RepairStatus.Diagnosing => "Arıza Tespiti",
            RepairStatus.WaitingForParts => "Parça Bekleniyor",
            RepairStatus.SentToFactory => "Fabrikada",
            RepairStatus.ReturnedFromFactory => "Fabrikadan Geldi",
            RepairStatus.InRepair => "Tamir Sürüyor",
            RepairStatus.Testing => "Test Aşaması",
            RepairStatus.ReadyForPickup => "Teslimata Hazır",
            RepairStatus.Delivered => "Teslim Edildi",
            RepairStatus.Unrepairable => "İade/Hurda",
            _ => RepairStatus.ToString()
        };

        public string StatusColor => RepairStatus switch
        {
            RepairStatus.Registered => "#9E9E9E",
            RepairStatus.Diagnosing => "#2196F3",
            RepairStatus.WaitingForParts => "#FF9800",
            RepairStatus.SentToFactory => "#9C27B0",
            RepairStatus.ReturnedFromFactory => "#673AB7",
            RepairStatus.InRepair => "#03A9F4",
            RepairStatus.Testing => "#00BCD4",
            RepairStatus.ReadyForPickup => "#4CAF50",
            RepairStatus.Delivered => "#8BC34A",
            RepairStatus.Unrepairable => "#F44336",
            _ => "#757575"
        };

        public string StatusBgColor => RepairStatus switch
        {
            RepairStatus.Registered => "#F5F5F5",
            RepairStatus.Diagnosing => "#E3F2FD",
            RepairStatus.WaitingForParts => "#FFF3E0",
            RepairStatus.SentToFactory => "#F3E5F5",
            RepairStatus.ReturnedFromFactory => "#EDE7F6",
            RepairStatus.InRepair => "#E1F5FE",
            RepairStatus.Testing => "#E0F7FA",
            RepairStatus.ReadyForPickup => "#E8F5E9",
            RepairStatus.Delivered => "#F1F8E9",
            RepairStatus.Unrepairable => "#FFEBEE",
            _ => "#FAFAFA"
        };
    }

    /// <summary>
    /// Status dropdown seçeneği
    /// </summary>
    public class RepairStatusOption
    {
        public RepairStatus? Status { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string FullDisplay => $"{Icon} {DisplayName}";
    }
}
