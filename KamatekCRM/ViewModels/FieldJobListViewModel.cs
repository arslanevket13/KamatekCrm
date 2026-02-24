using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Saha Operasyon & Montaj Listesi ViewModel
    /// </summary>
    public class FieldJobListViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private string _searchText = string.Empty;
        private DateTime? _startDate;
        private DateTime? _endDate;

        public ObservableCollection<FieldJobDisplayItem> AllFieldJobs { get; set; }
        public ICollectionView FilteredFieldJobs { get; private set; }
        public ObservableCollection<CategoryFilterItem> CategoryFilters { get; set; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    FilteredFieldJobs?.Refresh();
            }
        }

        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                    FilteredFieldJobs?.Refresh();
            }
        }

        public DateTime? EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                    FilteredFieldJobs?.Refresh();
            }
        }

        public int TotalCount => FilteredFieldJobs?.Cast<object>().Count() ?? 0;

        // Commands
        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand OpenMapCommand { get; }
        public ICommand ShowFieldPhotosCommand { get; }
        public ICommand CompleteFieldJobCommand { get; }
        public ICommand OpenFieldJobDetailCommand { get; }
        public ICommand ToggleCategoryCommand { get; }

        public FieldJobListViewModel()
        {
            _context = new AppDbContext();
            AllFieldJobs = new ObservableCollection<FieldJobDisplayItem>();
            CategoryFilters = new ObservableCollection<CategoryFilterItem>();

            // Kategori filtrelerini başlat
            InitializeCategoryFilters();

            // Commands
            RefreshCommand = new RelayCommand(_ => LoadFieldJobs());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            OpenMapCommand = new RelayCommand(ExecuteOpenMap);
            ShowFieldPhotosCommand = new RelayCommand(ExecuteShowPhotos);
            CompleteFieldJobCommand = new RelayCommand(ExecuteCompleteJob);
            OpenFieldJobDetailCommand = new RelayCommand(ExecuteOpenDetail);
            ToggleCategoryCommand = new RelayCommand(ExecuteToggleCategory);

            // CollectionView
            FilteredFieldJobs = CollectionViewSource.GetDefaultView(AllFieldJobs);
            FilteredFieldJobs.Filter = FilterFieldJobs;
            FilteredFieldJobs.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalCount));

            // Veriyi yükle
            LoadFieldJobs();
        }

        private void InitializeCategoryFilters()
        {
            CategoryFilters.Add(new CategoryFilterItem { Category = JobCategory.CCTV, DisplayName = "CCTV", Icon = "📹", IsSelected = false });
            CategoryFilters.Add(new CategoryFilterItem { Category = JobCategory.FireAlarm, DisplayName = "Yangın", Icon = "🔥", IsSelected = false });
            CategoryFilters.Add(new CategoryFilterItem { Category = JobCategory.BurglarAlarm, DisplayName = "Alarm", Icon = "🚨", IsSelected = false });
            CategoryFilters.Add(new CategoryFilterItem { Category = JobCategory.VideoIntercom, DisplayName = "Diafon", Icon = "🚪", IsSelected = false });
            CategoryFilters.Add(new CategoryFilterItem { Category = JobCategory.SatelliteSystem, DisplayName = "Uydu", Icon = "📡", IsSelected = false });
            CategoryFilters.Add(new CategoryFilterItem { Category = JobCategory.AccessControl, DisplayName = "PDKS", Icon = "🔑", IsSelected = false });
            CategoryFilters.Add(new CategoryFilterItem { Category = JobCategory.SmartHome, DisplayName = "Akıllı Ev", Icon = "🏠", IsSelected = false });
            CategoryFilters.Add(new CategoryFilterItem { Category = JobCategory.FiberOptic, DisplayName = "Fiber", Icon = "🌐", IsSelected = false });
        }

        private void LoadFieldJobs()
        {
            AllFieldJobs.Clear();

            try
            {
                var fieldJobs = _context.ServiceJobs
                    .Include(j => j.Customer)
                    .Where(j => j.WorkOrderType == WorkOrderType.Installation 
                             || j.WorkOrderType == WorkOrderType.Maintenance
                             || j.WorkOrderType == WorkOrderType.Inspection)
                    .OrderByDescending(j => j.ScheduledDate ?? j.CreatedDate)
                    .ToList();

                foreach (var job in fieldJobs)
                {
                    AllFieldJobs.Add(new FieldJobDisplayItem
                    {
                        Id = job.Id,
                        JobNo = $"#SJ-{job.Id:D4}",
                        CustomerName = job.Customer?.FullName ?? "Bilinmiyor",
                        Address = job.Customer?.FullAddress ?? "",
                        City = job.Customer?.City ?? "",
                        JobCategory = job.JobCategory,
                        WorkOrderType = job.WorkOrderType,
                        ScheduledDate = job.ScheduledDate,
                        AssignedTechnician = job.AssignedTechnician ?? "Atanmadı",
                        Status = job.Status,
                        Description = job.Description,
                        Priority = job.Priority
                    });
                }

                OnPropertyChanged(nameof(TotalCount));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veriler yüklenirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool FilterFieldJobs(object obj)
        {
            if (obj is not FieldJobDisplayItem job)
                return false;

            // Kategori filtresi (herhangi biri seçiliyse)
            var selectedCategories = CategoryFilters.Where(c => c.IsSelected).Select(c => c.Category).ToList();
            if (selectedCategories.Count > 0 && !selectedCategories.Contains(job.JobCategory))
                return false;

            // Tarih filtresi
            var jobDate = job.ScheduledDate ?? DateTime.MinValue;
            if (StartDate.HasValue && jobDate.Date < StartDate.Value.Date)
                return false;
            if (EndDate.HasValue && jobDate.Date > EndDate.Value.Date)
                return false;

            // Metin araması (Adres, Müşteri, Job No)
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLower();
                return job.Address.ToLower().Contains(search)
                    || job.City.ToLower().Contains(search)
                    || job.CustomerName.ToLower().Contains(search)
                    || job.JobNo.ToLower().Contains(search);
            }

            return true;
        }

        private void ExecuteToggleCategory(object? parameter)
        {
            if (parameter is CategoryFilterItem filter)
            {
                filter.IsSelected = !filter.IsSelected;
                FilteredFieldJobs?.Refresh();
                OnPropertyChanged(nameof(TotalCount));
            }
        }

        private void ClearFilters()
        {
            SearchText = string.Empty;
            StartDate = null;
            EndDate = null;
            foreach (var cat in CategoryFilters) cat.IsSelected = false;
            FilteredFieldJobs?.Refresh();
        }

        private void ExecuteOpenMap(object? parameter)
        {
            if (parameter is FieldJobDisplayItem job)
            {
                if (string.IsNullOrEmpty(job.Address))
                {
                    MessageBox.Show("Adres bilgisi bulunamadı.", "Harita", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var address = System.Uri.EscapeDataString($"{job.Address}, {job.City}, Türkiye");
                var url = $"https://www.google.com/maps/search/?api=1&query={address}";
                
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    MessageBox.Show($"Harita açılamadı.\n\nAdres: {job.Address}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteShowPhotos(object? parameter)
        {
            if (parameter is FieldJobDisplayItem job)
            {
                MessageBox.Show($"Montaj fotoğrafları özelliği yakında eklenecek.\n\nİş: {job.JobNo}", "Fotoğraflar", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExecuteCompleteJob(object? parameter)
        {
            if (parameter is FieldJobDisplayItem job)
            {
                var result = MessageBox.Show(
                    $"İşi tamamlamak istiyor musunuz?\n\n{job.JobNo} - {job.CustomerName}",
                    "İşi Tamamla",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var dbJob = _context.ServiceJobs.Find(job.Id);
                        if (dbJob != null)
                        {
                            dbJob.Status = JobStatus.Completed;
                            dbJob.CompletedDate = DateTime.Now;
                            _context.SaveChanges();
                            
                            job.Status = JobStatus.Completed;
                            FilteredFieldJobs?.Refresh();
                            
                            MessageBox.Show("İş başarıyla tamamlandı!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ExecuteOpenDetail(object? parameter)
        {
            if (parameter is FieldJobDisplayItem job)
            {
                MessageBox.Show($"Saha işi detay penceresi: {job.JobNo}", "Detay", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    /// <summary>
    /// Saha işi görüntüleme modeli
    /// </summary>
    public class FieldJobDisplayItem
    {
        public int Id { get; set; }
        public string JobNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public JobCategory JobCategory { get; set; }
        public WorkOrderType WorkOrderType { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public string AssignedTechnician { get; set; } = string.Empty;
        public JobStatus Status { get; set; }
        public string Description { get; set; } = string.Empty;
        public JobPriority Priority { get; set; }

        // Computed
        public string LocationDisplay => !string.IsNullOrEmpty(City) ? $"{Address}, {City}" : Address;
        public string ScheduleDisplay => ScheduledDate?.ToString("dd MMM yyyy HH:mm") ?? "Planlanmadı";
        
        public string CategoryIcon => JobCategory switch
        {
            JobCategory.CCTV => "📹",
            JobCategory.FireAlarm => "🔥",
            JobCategory.BurglarAlarm => "🚨",
            JobCategory.VideoIntercom => "🚪",
            JobCategory.SatelliteSystem => "📡",
            JobCategory.AccessControl => "🔑",
            JobCategory.SmartHome => "🏠",
            JobCategory.FiberOptic => "🌐",
            _ => "📋"
        };

        public string CategoryDisplay => JobCategory switch
        {
            JobCategory.CCTV => "CCTV",
            JobCategory.FireAlarm => "Yangın",
            JobCategory.BurglarAlarm => "Alarm",
            JobCategory.VideoIntercom => "Diafon",
            JobCategory.SatelliteSystem => "Uydu",
            JobCategory.AccessControl => "PDKS",
            JobCategory.SmartHome => "Akıllı Ev",
            JobCategory.FiberOptic => "Fiber",
            _ => JobCategory.ToString()
        };

        public string CategoryColor => JobCategory switch
        {
            JobCategory.CCTV => "#1976D2",
            JobCategory.FireAlarm => "#D32F2F",
            JobCategory.BurglarAlarm => "#F57C00",
            JobCategory.VideoIntercom => "#7B1FA2",
            JobCategory.SatelliteSystem => "#00796B",
            JobCategory.AccessControl => "#5D4037",
            JobCategory.SmartHome => "#0097A7",
            JobCategory.FiberOptic => "#388E3C",
            _ => "#757575"
        };

        public string StatusDisplay => Status switch
        {
            JobStatus.Pending => "Bekliyor",
            JobStatus.InProgress => "Devam Ediyor",
            JobStatus.Completed => "Tamamlandı",
            JobStatus.Cancelled => "İptal",
            _ => Status.ToString()
        };

        public bool IsCompleted => Status == JobStatus.Completed;
    }

    /// <summary>
    /// Kategori filtre öğesi
    /// </summary>
    public class CategoryFilterItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public JobCategory Category { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public string FullDisplay => $"{Icon} {DisplayName}";

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
