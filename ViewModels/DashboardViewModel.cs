using System;
using System.Collections.ObjectModel;
using System.Linq;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Dashboard ViewModel - Ana sayfa iş zekası ve özet bilgiler
    /// </summary>
    public class DashboardViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;

        #region KPI Counters

        private int _activeJobsCount;
        /// <summary>
        /// Aktif iş sayısı (Status != Completed)
        /// </summary>
        public int ActiveJobsCount
        {
            get => _activeJobsCount;
            set => SetProperty(ref _activeJobsCount, value);
        }

        private int _criticalStockCount;
        /// <summary>
        /// Kritik stok sayısı (TotalStockQuantity <= MinStockLevel)
        /// </summary>
        public int CriticalStockCount
        {
            get => _criticalStockCount;
            set => SetProperty(ref _criticalStockCount, value);
        }

        private int _totalCustomersCount;
        /// <summary>
        /// Toplam müşteri sayısı
        /// </summary>
        public int TotalCustomersCount
        {
            get => _totalCustomersCount;
            set => SetProperty(ref _totalCustomersCount, value);
        }

        private int _monthlyJobsCount;
        /// <summary>
        /// Bu ay oluşturulan iş sayısı
        /// </summary>
        public int MonthlyJobsCount
        {
            get => _monthlyJobsCount;
            set => SetProperty(ref _monthlyJobsCount, value);
        }

        #endregion

        #region Collections

        /// <summary>
        /// Acil İşler - Priority == Urgent/Critical ve Status != Completed
        /// </summary>
        public ObservableCollection<ServiceJob> UrgentJobs { get; set; } = new();

        /// <summary>
        /// Son Stok Hareketleri - Son 10 hareket
        /// </summary>
        public ObservableCollection<StockTransaction> RecentTransactions { get; set; } = new();

        /// <summary>
        /// Yeni Müşteriler - Son 5 eklenen
        /// </summary>
        public ObservableCollection<Customer> NewCustomers { get; set; } = new();

        /// <summary>
        /// Kritik Stoklar - TotalStockQuantity <= MinStockLevel
        /// </summary>
        public ObservableCollection<Product> CriticalStocks { get; set; } = new();

        #endregion

        #region Display Properties

        /// <summary>
        /// Kullanıcı karşılama metni
        /// </summary>
        public string WelcomeMessage => "Hoşgeldin, Admin";

        /// <summary>
        /// Bugünün tarihi (Türkçe format)
        /// </summary>
        public string TodayDate => DateTime.Now.ToString("dd MMMM yyyy, dddd", new System.Globalization.CultureInfo("tr-TR"));

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public DashboardViewModel()
        {
            _context = new AppDbContext();
            LoadDashboardData();
        }

        /// <summary>
        /// Dashboard verilerini yükle
        /// </summary>
        private void LoadDashboardData()
        {
            LoadKPICounters();
            LoadUrgentJobs();
            LoadRecentTransactions();
            LoadNewCustomers();
            LoadCriticalStocks();
        }

        /// <summary>
        /// KPI sayaçlarını yükle
        /// </summary>
        private void LoadKPICounters()
        {
            // Aktif İş Sayısı (Tamamlanmamış)
            ActiveJobsCount = _context.ServiceJobs
                .Count(j => j.Status != JobStatus.Completed);

            // Kritik Stok Sayısı
            CriticalStockCount = _context.Products
                .Count(p => p.TotalStockQuantity <= p.MinStockLevel && p.MinStockLevel > 0);

            // Toplam Müşteri Sayısı
            TotalCustomersCount = _context.Customers.Count();

            // Bu Ay Oluşturulan İşler
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            MonthlyJobsCount = _context.ServiceJobs
                .Count(j => j.CreatedDate >= startOfMonth);
        }

        /// <summary>
        /// Acil işleri yükle (Urgent/Critical priority, henüz tamamlanmamış)
        /// </summary>
        private void LoadUrgentJobs()
        {
            var urgentJobs = _context.ServiceJobs
                .Include(j => j.Customer)
                .Where(j => (j.Priority == JobPriority.Urgent || j.Priority == JobPriority.Critical)
                         && j.Status != JobStatus.Completed)
                .OrderBy(j => j.ScheduledDate ?? j.CreatedDate)
                .Take(10)
                .ToList();

            UrgentJobs.Clear();
            foreach (var job in urgentJobs)
            {
                UrgentJobs.Add(job);
            }
        }

        /// <summary>
        /// Son stok hareketlerini yükle
        /// </summary>
        private void LoadRecentTransactions()
        {
            var recentTransactions = _context.StockTransactions
                .Include(t => t.Product)
                .Include(t => t.SourceWarehouse)
                .Include(t => t.TargetWarehouse)
                .OrderByDescending(t => t.Date)
                .Take(10)
                .ToList();

            RecentTransactions.Clear();
            foreach (var transaction in recentTransactions)
            {
                RecentTransactions.Add(transaction);
            }
        }

        /// <summary>
        /// Son eklenen müşterileri yükle
        /// </summary>
        private void LoadNewCustomers()
        {
            var newCustomers = _context.Customers
                .OrderByDescending(c => c.Id) // ID'ye göre son eklenenler
                .Take(5)
                .ToList();

            NewCustomers.Clear();
            foreach (var customer in newCustomers)
            {
                NewCustomers.Add(customer);
            }
        }

        /// <summary>
        /// Kritik stokları yükle
        /// </summary>
        private void LoadCriticalStocks()
        {
            var criticalStocks = _context.Products
                .Where(p => p.TotalStockQuantity <= p.MinStockLevel && p.MinStockLevel > 0)
                .OrderBy(p => p.TotalStockQuantity)
                .Take(10)
                .ToList();

            CriticalStocks.Clear();
            foreach (var product in criticalStocks)
            {
                CriticalStocks.Add(product);
            }
        }
    }
}
