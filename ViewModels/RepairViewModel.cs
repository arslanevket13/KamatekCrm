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
            
            LoadData();
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

        #endregion

        #region Commands

        public ICommand SaveNewRepairCommand { get; }
        public ICommand UpdateStatusCommand { get; }
        public ICommand AddNoteCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenRegistrationCommand { get; }



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

            OnPropertyChanged(nameof(PendingRepairs));
            OnPropertyChanged(nameof(InProgressRepairs));
            OnPropertyChanged(nameof(CompletedRepairs));
        }

        private void LoadHistory(int jobId)
        {
            if (jobId == 0)
            {
                JobHistory.Clear();
                return;
            }

            var history = _context.ServiceJobHistories
                .Where(h => h.ServiceJobId == jobId)
                .OrderByDescending(h => h.Date)
                .ToList();

            JobHistory = new ObservableCollection<ServiceJobHistory>(history);
        }

        private void OpenRegistration(object? parameter)
        {
            // Yeni pencere açma işlemi View katmanında yapılacak code-behind veya servis aracılığıyla yapılır.
            // MVVM kuralını esneterek şimdilik basit tutuyoruz, bu command View'dan tetiklenecek
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

        #endregion
    }
}
