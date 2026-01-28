using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;

using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    public class RepairViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;

        public RepairViewModel()
        {
            _context = new AppDbContext();
            
            // Komutlar
            SaveNewRepairCommand = new RelayCommand(SaveNewRepair, CanSaveNewRepair);
            UpdateStatusCommand = new RelayCommand<RepairStatus?>(UpdateStatus);
            AddNoteCommand = new RelayCommand(AddNote, _ => SelectedJob != null && !string.IsNullOrWhiteSpace(NewNoteText));
            RefreshCommand = new RelayCommand(_ => LoadData());
            OpenRegistrationCommand = new RelayCommand(OpenRegistration);
            AddItemToJobCommand = new RelayCommand(AddItemToJob);
            RemoveItemFromJobCommand = new RelayCommand(RemoveItemFromJob);
            CompleteJobCommand = new RelayCommand(CompleteJob);
            PrintServiceFormCommand = new RelayCommand(PrintServiceForm);
            
            LoadData();
            UpdateDeviceTypeOptions();
        }

        private decimal _laborCost;
        public decimal LaborCost
        {
            get => _laborCost;
            set
            {
                if (SetProperty(ref _laborCost, value)) UpdateTotals();
            }
        }

        private decimal _discountAmount;
        public decimal DiscountAmount
        {
            get => _discountAmount;
            set
            {
                if (SetProperty(ref _discountAmount, value)) UpdateTotals();
            }
        }

        public decimal MaterialTotal => CurrentJobItems.Sum(x => x.UnitPrice * x.QuantityUsed);
        public decimal GrandTotal => MaterialTotal + LaborCost - DiscountAmount;

        public ObservableCollection<ServiceJobItem> CurrentJobItems { get; set; } = new ObservableCollection<ServiceJobItem>();
        public ObservableCollection<Product> Products { get; set; } = new ObservableCollection<Product>();

        private Product? _selectedProductToAdd;
        public Product? SelectedProductToAdd
        {
            get => _selectedProductToAdd;
            set
            {
                if (SetProperty(ref _selectedProductToAdd, value) && value != null)
                {
                    UnitPriceToAdd = value.SalePrice;
                }
            }
        }

        private int _quantityToAdd = 1;
        public int QuantityToAdd
        {
            get => _quantityToAdd;
            set => SetProperty(ref _quantityToAdd, value);
        }

        private decimal _unitPriceToAdd;
        public decimal UnitPriceToAdd
        {
            get => _unitPriceToAdd;
            set => SetProperty(ref _unitPriceToAdd, value);
        }

        #region Properties (List & Detail)

        private ObservableCollection<ServiceJob> _allRepairs = new();
        public ObservableCollection<ServiceJob> AllRepairs
        {
            get => _allRepairs;
            set => SetProperty(ref _allRepairs, value);
        }

        // Gruplandırma için CollectionView kullanılabilir ama şimdilik ViewModel'de filtreleyelim
        public IEnumerable<ServiceJob> PendingRepairs => AllRepairs.Where(x => x.RepairStatus == RepairStatus.Registered || x.RepairStatus == RepairStatus.Diagnosing);
        public IEnumerable<ServiceJob> InProgressRepairs => AllRepairs.Where(x => x.RepairStatus == RepairStatus.InRepair || x.RepairStatus == RepairStatus.WaitingForParts || x.RepairStatus == RepairStatus.SentToFactory);
        public IEnumerable<ServiceJob> CompletedRepairs => AllRepairs.Where(x => x.RepairStatus == RepairStatus.ReadyForPickup || x.RepairStatus == RepairStatus.Delivered || x.RepairStatus == RepairStatus.Unrepairable);


        private ServiceJob? _selectedJob;
        public ServiceJob? SelectedJob
        {
            get => _selectedJob;
            set
            {
                if (SetProperty(ref _selectedJob, value))
                {
                    LoadHistory(value?.Id ?? 0);
                    OnPropertyChanged(nameof(IsJobSelected));
                    // Yeni not alanını temizle
                    NewNoteText = string.Empty;
                }
            }
        }

        public bool IsJobSelected => SelectedJob != null;

        private ObservableCollection<ServiceJobHistory> _jobHistory = new();
        public ObservableCollection<ServiceJobHistory> JobHistory
        {
            get => _jobHistory;
            set => SetProperty(ref _jobHistory, value);
        }

        private string _newNoteText = string.Empty;
        public string NewNoteText
        {
            get => _newNoteText;
            set => SetProperty(ref _newNoteText, value);
        }

        #endregion

        #region Properties (Registration Form)

        // Yeni Kayıt Formu için alanlar
        private ServiceJob _newJob = new() { ServiceJobType = ServiceJobType.Fault };
        public ServiceJob NewJob
        {
            get => _newJob;
            set => SetProperty(ref _newJob, value);
        }

        private Customer? _selectedCustomerForNewJob;
        public Customer? SelectedCustomerForNewJob
        {
            get => _selectedCustomerForNewJob;
            set
            {
                if (SetProperty(ref _selectedCustomerForNewJob, value) && value != null)
                {
                    NewJob.CustomerId = value.Id;
                }
            }
        }

        public ObservableCollection<Customer> Customers { get; } = new();
        
        // Yeni: Cihaz tipi seçenekleri
        public ObservableCollection<string> DeviceTypeOptions { get; } = new();

        // === MODERN UI PROPERTİES ===
        
        private bool _isCameraCategory = true;
        public bool IsCameraCategory
        {
            get => _isCameraCategory;
            set
            {
                if (SetProperty(ref _isCameraCategory, value))
                {
                    if (value) 
                    {
                        IsDiafonCategory = false;
                        NewJob.JobCategory = JobCategory.CCTV;
                    }
                    UpdateDeviceTypeOptions();
                }
            }
        }

        private bool _isDiafonCategory;
        public bool IsDiafonCategory
        {
            get => _isDiafonCategory;
            set
            {
                if (SetProperty(ref _isDiafonCategory, value))
                {
                    if (value) 
                    {
                        IsCameraCategory = false;
                        NewJob.JobCategory = JobCategory.VideoIntercom;
                    }
                    UpdateDeviceTypeOptions();
                }
            }
        }

        private string _selectedDeviceTypeName = string.Empty;
        public string SelectedDeviceTypeName
        {
            get => _selectedDeviceTypeName;
            set => SetProperty(ref _selectedDeviceTypeName, value);
        }

        // Aksesuarlar
        private bool _accessoryAdapter;
        public bool AccessoryAdapter
        {
            get => _accessoryAdapter;
            set => SetProperty(ref _accessoryAdapter, value);
        }

        private bool _accessoryCable;
        public bool AccessoryCable
        {
            get => _accessoryCable;
            set => SetProperty(ref _accessoryCable, value);
        }

        private bool _accessoryRemote;
        public bool AccessoryRemote
        {
            get => _accessoryRemote;
            set => SetProperty(ref _accessoryRemote, value);
        }

        // Hızlı müşteri ekleme
        private bool _isQuickAddCustomer;
        public bool IsQuickAddCustomer
        {
            get => _isQuickAddCustomer;
            set => SetProperty(ref _isQuickAddCustomer, value);
        }

        private string _quickCustomerName = string.Empty;
        public string QuickCustomerName
        {
            get => _quickCustomerName;
            set => SetProperty(ref _quickCustomerName, value);
        }

        private string _quickCustomerPhone = string.Empty;
        public string QuickCustomerPhone
        {
            get => _quickCustomerPhone;
            set => SetProperty(ref _quickCustomerPhone, value);
        }

        #endregion

        #region Commands

        public ICommand SaveNewRepairCommand { get; }
        public ICommand UpdateStatusCommand { get; }
        public ICommand AddNoteCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenRegistrationCommand { get; }



        public ICommand AddItemToJobCommand { get; }
        public ICommand RemoveItemFromJobCommand { get; }
        public ICommand CompleteJobCommand { get; }
        public ICommand PrintServiceFormCommand { get; }

        public void SelectJobById(int id)
        {
            var job = AllRepairs.FirstOrDefault(x => x.Id == id);
            if (job != null)
            {
                SelectedJob = job;
            }
        }

        #endregion

        #region Methods

        private void LoadData()
        {
            var repairs = _context.ServiceJobs
                .Include(j => j.Customer)
                .Where(j => j.ServiceJobType == ServiceJobType.Fault) // Sadece Arıza kayıtları
                .OrderByDescending(j => j.CreatedDate)
                .ToList();

            AllRepairs = new ObservableCollection<ServiceJob>(repairs);
            
            // Müşterileri de yükle (Registration için)
            var customers = _context.Customers.OrderBy(c => c.FullName).ToList();
            Customers.Clear();
            foreach(var c in customers) Customers.Add(c);

            // Ürünleri yükle (Parça değişimi için)
            LoadProducts();

            OnPropertyChanged(nameof(PendingRepairs));
            OnPropertyChanged(nameof(InProgressRepairs));
            OnPropertyChanged(nameof(CompletedRepairs));
        }

        private void LoadProducts()
        {
            Products.Clear();
            var products = _context.Products.OrderBy(p => p.ProductName).ToList();
            foreach (var p in products) Products.Add(p);
        }

        private void UpdateDeviceTypeOptions()
        {
            DeviceTypeOptions.Clear();
            
            if (IsCameraCategory)
            {
                DeviceTypeOptions.Add("DVR");
                DeviceTypeOptions.Add("NVR");
                DeviceTypeOptions.Add("IP Kamera");
                DeviceTypeOptions.Add("Analog Kamera");
                DeviceTypeOptions.Add("PTZ Kamera");
                DeviceTypeOptions.Add("Speed Dome");
                DeviceTypeOptions.Add("Monitor");
            }
            else if (IsDiafonCategory)
            {
                DeviceTypeOptions.Add("Diafon Paneli");
                DeviceTypeOptions.Add("Diafon Dairesi");
                DeviceTypeOptions.Add("Görüntülü Diafon");
                DeviceTypeOptions.Add("Zil Paneli");
                DeviceTypeOptions.Add("Santral");
            }
        }

        private void LoadHistory(int jobId)
        {
            if (jobId == 0)
            {
                JobHistory.Clear();
                CurrentJobItems.Clear();
                return;
            }

            var history = _context.ServiceJobHistories
                .Where(h => h.ServiceJobId == jobId)
                .OrderByDescending(h => h.Date)
                .ToList();

            JobHistory = new ObservableCollection<ServiceJobHistory>(history);

            // Parçaları yükle
            LoadJobItems(jobId);
        }

        private void LoadJobItems(int jobId)
        {
            CurrentJobItems.Clear();
            var items = _context.ServiceJobItems
                .Include(i => i.Product)
                .Where(i => i.ServiceJobId == jobId)
                .ToList();

            foreach(var item in items) CurrentJobItems.Add(item);
            
            // Fiyatları güncelle
            if (SelectedJob != null)
            {
                 // Eğer DB'de kayıtlı değerler 0 ise hesapla, yoksa kaydet
                 // Basitlik için UI'da gösterilen değerleri SelectedJob üzerinden alıyoruz
                 // Değişiklikler Save/Update ile kaydedilmeli
            }
            UpdateTotals();
        }

        private void OpenRegistration(object? parameter)
        {
            ResetNewJobForm();
        }

        private void ResetNewJobForm()
        {
            NewJob = new ServiceJob 
            { 
                ServiceJobType = ServiceJobType.Fault,
                CreatedDate = DateTime.Now,
                RepairStatus = RepairStatus.Registered,
                Status = JobStatus.Pending,
                WorkOrderType = WorkOrderType.Repair,
                JobCategory = JobCategory.CCTV // Default
            };
            SelectedCustomerForNewJob = null;
        }

        private bool CanSaveNewRepair(object? parameter)
        {
            return SelectedCustomerForNewJob != null 
                && !string.IsNullOrWhiteSpace(NewJob.Description)
                && !string.IsNullOrWhiteSpace(NewJob.DeviceBrand)
                && !string.IsNullOrWhiteSpace(NewJob.DeviceModel);
        }

        private void SaveNewRepair(object? parameter)
        {
            try
            {
                _context.ServiceJobs.Add(NewJob);
                _context.SaveChanges(); // ID oluşması için

                // İlk tarihçe kaydı
                var history = new ServiceJobHistory
                {
                    ServiceJobId = NewJob.Id,
                    Date = DateTime.Now,
                    StatusChange = RepairStatus.Registered,
                    TechnicianNote = "Cihaz kabul edildi.",
                    UserId = "System" // Mevcut kullanıcı sistemi entegre edilebilir
                };
                _context.ServiceJobHistories.Add(history);
                _context.SaveChanges();

                MessageBox.Show($"Cihaz kabul edildi! Takip No: {NewJob.Id}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Formu sıfırla ve listeyi güncelle
                ResetNewJobForm();
                LoadData();

                // Eğer pencere ise kapat (parametre Window ise)
                if (parameter is Window w) w.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void UpdateStatus(RepairStatus? newStatus)
        {
            if (SelectedJob == null || newStatus == null) return;

             var oldStatus = SelectedJob.RepairStatus;
            SelectedJob.RepairStatus = newStatus.Value;
            
            // ServiceJob.Status (Genel) mapping
            if (newStatus == RepairStatus.Delivered) SelectedJob.Status = JobStatus.Completed;
            else if (newStatus == RepairStatus.Unrepairable) SelectedJob.Status = JobStatus.Cancelled;
            else SelectedJob.Status = JobStatus.InProgress;

            if (newStatus == RepairStatus.ReadyForPickup || newStatus == RepairStatus.Delivered)
            {
                // Fiyatları kaydet
                SelectedJob.LaborCost = LaborCost;
                SelectedJob.DiscountAmount = DiscountAmount;
            }

            _context.ServiceJobs.Update(SelectedJob);

            // Tarihçe ekle
            var history = new ServiceJobHistory
            {
                ServiceJobId = SelectedJob.Id,
                Date = DateTime.Now,
                StatusChange = newStatus.Value,
                TechnicianNote = !string.IsNullOrWhiteSpace(NewNoteText) ? NewNoteText : $"Durum değişikliği: {oldStatus} -> {newStatus}",
                UserId = "Technician" // TODO: Current User
            };
            _context.ServiceJobHistories.Add(history);
            _context.SaveChanges();

            // ═══════════════════════════════════════════════════════════════════
            // KASA ENTEGRASYONU: Teslim edildiğinde geliri CashTransaction'a kaydet
            // ═══════════════════════════════════════════════════════════════════
            if (newStatus == RepairStatus.Delivered && SelectedJob.TotalAmount > 0)
            {
                var cashTransaction = new CashTransaction
                {
                    Date = DateTime.Now,
                    Amount = SelectedJob.TotalAmount,
                    TransactionType = CashTransactionType.CashIncome, // Varsayılan nakit
                    Description = $"Tamir Teslimi - İş #{SelectedJob.Id}",
                    Category = "Teknik Servis",
                    ReferenceNumber = $"REP-{SelectedJob.Id}",
                    CustomerId = SelectedJob.CustomerId,
                    CreatedBy = AuthService.CurrentUser?.AdSoyad ?? "Teknisyen",
                    CreatedAt = DateTime.Now
                };
                _context.CashTransactions.Add(cashTransaction);
                _context.SaveChanges();
            }

            // SMS Bildirimi (Otomatik)
            if (newStatus == RepairStatus.ReadyForPickup && SelectedJob.Customer != null)
            {
                if (!string.IsNullOrWhiteSpace(SelectedJob.Customer.PhoneNumber))
                {
                    try
                    {
                        var smsService = new SmsService();
                        string msg = $"Sayın {SelectedJob.Customer.FullName}, cihazınızın (Takip No: {SelectedJob.Id}) tamir işlemleri tamamlanmıştır. Teslim alabilirsiniz. Kamatek Teknik Servis";
                        await smsService.SendSmsAsync(SelectedJob.Customer.PhoneNumber, msg);
                        MessageBox.Show("Müşteriye otomatik SMS bildirimi gönderildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"SMS gönderimi başarısız: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }

            NewNoteText = string.Empty; // Notu temizle
            LoadHistory(SelectedJob.Id);
            LoadData(); // Listeleri güncelle (Gruplama değiştiği için)
        }

        private void AddNote(object? parameter)
        {
            if (SelectedJob == null) return;

            var history = new ServiceJobHistory
            {
                ServiceJobId = SelectedJob.Id,
                Date = DateTime.Now,
                TechnicianNote = NewNoteText,
                UserId = "Technician"
            };
            _context.ServiceJobHistories.Add(history);
            _context.SaveChanges();

            NewNoteText = string.Empty;
            LoadHistory(SelectedJob.Id);
        }

        // ==========================================
        // PARÇA VE MALİYET YÖNETİMİ
        // ==========================================

        private void AddItemToJob(object? parameter)
        {
            if (SelectedJob == null || SelectedProductToAdd == null) return;

            var newItem = new ServiceJobItem
            {
                ServiceJobId = SelectedJob.Id,
                ProductId = SelectedProductToAdd.Id,
                Product = SelectedProductToAdd, // Navigation prop
                QuantityUsed = QuantityToAdd,
                UnitPrice = UnitPriceToAdd,
                UnitCost = SelectedProductToAdd.PurchasePrice
            };

            _context.ServiceJobItems.Add(newItem);
            _context.SaveChanges(); // DB'ye hemen ekle

            CurrentJobItems.Add(newItem);
            
            // Stok güncellemesi (opsiyonel hemen düşülmesi gerekiyorsa)
            // Şimdilik "Complete" aşamasında düşme mantığı korunabilir veya burada düşülebilir.
            // ServiceJobViewModel'da CompleteJob'da düşülüyordu. Burada da aynısını yapalım.

            SelectedProductToAdd = null;
            QuantityToAdd = 1;
            UnitPriceToAdd = 0;
            UpdateTotals();
        }

        private void RemoveItemFromJob(object? parameter)
        {
            if (parameter is ServiceJobItem item)
            {
                _context.ServiceJobItems.Remove(item);
                _context.SaveChanges();
                CurrentJobItems.Remove(item);
                UpdateTotals();
            }
        }

        private void UpdateTotals()
        {
            if (SelectedJob == null) return;

            OnPropertyChanged(nameof(MaterialTotal));
            OnPropertyChanged(nameof(GrandTotal));
            
            // DB Update (Fiyatlar değiştikçe kaydetmek iyi olabilir)
            SelectedJob.LaborCost = LaborCost;
            SelectedJob.DiscountAmount = DiscountAmount;
            _context.ServiceJobs.Update(SelectedJob);
            _context.SaveChanges();
        }

        private void CompleteJob(object? parameter)
        {
            // Stok düşme işlemi ve 'Delivered' statüsüne çekme
            // Mevcut UpdateStatus('Delivered') çağrılabilir
            UpdateStatus(RepairStatus.Delivered);

             // Stok düşme işlemini buraya da ekleyebiliriz
             foreach(var item in CurrentJobItems)
             {
                 var product = _context.Products.Find(item.ProductId);
                 if (product != null)
                 {
                     product.TotalStockQuantity -= item.QuantityUsed;
                 }
             }
             _context.SaveChanges();
        }

        private void PrintServiceForm(object? parameter)
        {
            if (SelectedJob == null) return;
            
            try
            {
                // PDF Servisi kullan
                 var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Servis Fişini Kaydet",
                    Filter = "PDF Dosyası (*.pdf)|*.pdf",
                    FileName = $"ServisFisi_{SelectedJob.Id}.pdf"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // Tam data (Items yüklü olmalı)
                    var pdfService = new PdfService();
                    pdfService.GenerateServiceForm(SelectedJob, saveDialog.FileName);

                     var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveDialog.FileName,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(processInfo);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yazdırma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
