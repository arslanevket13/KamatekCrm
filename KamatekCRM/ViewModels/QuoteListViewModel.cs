using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Services;
using KamatekCrm.Shared.Models;
using KamatekCrm.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Teklif Listesi ViewModel — Tüm tekliflerin listelenmesi, filtrelenmesi, yönetilmesi
    /// </summary>
    public class QuoteListViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private readonly IServiceProvider _serviceProvider;

        #region Properties

        public ObservableCollection<ServiceProject> Quotes { get; } = new();

        private ICollectionView? _quotesView;
        public ICollectionView? QuotesView
        {
            get => _quotesView;
            private set => SetProperty(ref _quotesView, value);
        }

        private ServiceProject? _selectedQuote;
        public ServiceProject? SelectedQuote
        {
            get => _selectedQuote;
            set
            {
                if (SetProperty(ref _selectedQuote, value))
                {
                    OnPropertyChanged(nameof(HasSelectedQuote));
                }
            }
        }

        public bool HasSelectedQuote => SelectedQuote != null;

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    QuotesView?.Refresh();
                }
            }
        }

        private QuoteStatus? _statusFilter;
        public QuoteStatus? StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (SetProperty(ref _statusFilter, value))
                {
                    QuotesView?.Refresh();
                    OnPropertyChanged(nameof(StatusFilterDisplay));
                }
            }
        }

        public string StatusFilterDisplay => StatusFilter switch
        {
            QuoteStatus.Draft => "📝 Taslak",
            QuoteStatus.Sent => "📨 Gönderildi",
            QuoteStatus.Approved => "✅ Onaylandı",
            QuoteStatus.Rejected => "❌ Reddedildi",
            QuoteStatus.Expired => "⏰ Süresi Doldu",
            QuoteStatus.Revised => "🔄 Revize",
            _ => "Tümü"
        };

        // KPI properties
        public int TotalQuoteCount => Quotes.Count;
        public int DraftCount => Quotes.Count(q => q.QuoteStatus == QuoteStatus.Draft);
        public int SentCount => Quotes.Count(q => q.QuoteStatus == QuoteStatus.Sent);
        public int ApprovedCount => Quotes.Count(q => q.QuoteStatus == QuoteStatus.Approved);
        public int RejectedCount => Quotes.Count(q => q.QuoteStatus == QuoteStatus.Rejected);
        public decimal TotalApprovedAmount => Quotes.Where(q => q.QuoteStatus == QuoteStatus.Approved).Sum(q => q.TotalBudget);
        public decimal TotalPendingAmount => Quotes.Where(q => q.QuoteStatus == QuoteStatus.Sent).Sum(q => q.TotalBudget);

        public string TotalApprovedAmountDisplay => $"₺{TotalApprovedAmount:N0}";
        public string TotalPendingAmountDisplay => $"₺{TotalPendingAmount:N0}";

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isActionSuccessful;
        public bool IsActionSuccessful
        {
            get => _isActionSuccessful;
            set => SetProperty(ref _isActionSuccessful, value);
        }

        #endregion

        #region Commands

        public ICommand NewQuoteCommand { get; }
        public ICommand EditQuoteCommand { get; }
        public ICommand DuplicateQuoteCommand { get; }
        public ICommand DeleteQuoteCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand MarkAsSentCommand { get; }
        public ICommand MarkAsApprovedCommand { get; }
        public ICommand MarkAsRejectedCommand { get; }
        public ICommand FilterAllCommand { get; }
        public ICommand FilterDraftCommand { get; }
        public ICommand FilterSentCommand { get; }
        public ICommand FilterApprovedCommand { get; }
        public ICommand FilterRejectedCommand { get; }
        public ICommand RefreshCommand { get; }

        #endregion

        #region Constructor

        public QuoteListViewModel(AppDbContext context, IServiceProvider serviceProvider)
        {
            _context = context;
            _serviceProvider = serviceProvider;

            NewQuoteCommand = new RelayCommand(_ => NewQuote());
            EditQuoteCommand = new RelayCommand(_ => EditQuote(), _ => HasSelectedQuote);
            DuplicateQuoteCommand = new RelayCommand(_ => DuplicateQuote(), _ => HasSelectedQuote);
            DeleteQuoteCommand = new RelayCommand(_ => DeleteQuote(), _ => HasSelectedQuote);
            ExportPdfCommand = new RelayCommand(_ => ExportPdf(), _ => HasSelectedQuote);
            MarkAsSentCommand = new RelayCommand(_ => ChangeStatus(QuoteStatus.Sent), _ => SelectedQuote?.QuoteStatus == QuoteStatus.Draft);
            MarkAsApprovedCommand = new RelayCommand(_ => ChangeStatus(QuoteStatus.Approved), _ => SelectedQuote?.QuoteStatus == QuoteStatus.Sent);
            MarkAsRejectedCommand = new RelayCommand(_ => ChangeStatus(QuoteStatus.Rejected), _ => SelectedQuote?.QuoteStatus == QuoteStatus.Sent);
            FilterAllCommand = new RelayCommand(_ => StatusFilter = null);
            FilterDraftCommand = new RelayCommand(_ => StatusFilter = QuoteStatus.Draft);
            FilterSentCommand = new RelayCommand(_ => StatusFilter = QuoteStatus.Sent);
            FilterApprovedCommand = new RelayCommand(_ => StatusFilter = QuoteStatus.Approved);
            FilterRejectedCommand = new RelayCommand(_ => StatusFilter = QuoteStatus.Rejected);
            RefreshCommand = new RelayCommand(_ => LoadQuotes());

            LoadQuotes();
        }

        #endregion

        #region Data Loading

        private void LoadQuotes()
        {
            try
            {
                var quotes = _context.ServiceProjects
                    .Include(p => p.Customer)
                    .OrderByDescending(p => p.CreatedDate)
                    .ToList();

                Quotes.Clear();
                foreach (var q in quotes)
                    Quotes.Add(q);

                // Setup CollectionView with filter
                QuotesView = CollectionViewSource.GetDefaultView(Quotes);
                QuotesView.Filter = FilterQuotes;

                NotifyKpiChanged();

                StatusMessage = $"{Quotes.Count} teklif yüklendi.";
                IsActionSuccessful = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Teklifler yüklenirken hata: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        private bool FilterQuotes(object obj)
        {
            if (obj is not ServiceProject quote) return false;

            // Status filter
            if (StatusFilter.HasValue && quote.QuoteStatus != StatusFilter.Value)
                return false;

            // Text search
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLowerInvariant();
                return (quote.Title?.ToLowerInvariant().Contains(search) ?? false)
                    || (quote.ProjectCode?.ToLowerInvariant().Contains(search) ?? false)
                    || (quote.Customer?.FullName?.ToLowerInvariant().Contains(search) ?? false)
                    || (quote.QuoteNumber?.ToLowerInvariant().Contains(search) ?? false);
            }

            return true;
        }

        #endregion

        #region Quote Operations

        private void NewQuote()
        {
            try
            {
                var window = _serviceProvider.GetRequiredService<ProjectQuoteEditorWindow>();
                window.ShowDialog();
                LoadQuotes(); // Refresh after close
            }
            catch (Exception ex)
            {
                StatusMessage = $"Yeni teklif açılamadı: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        private void EditQuote()
        {
            if (SelectedQuote == null) return;

            try
            {
                var window = _serviceProvider.GetRequiredService<ProjectQuoteEditorWindow>();
                if (window.DataContext is ProjectQuoteEditorViewModel vm)
                {
                    vm.LoadExistingProject(SelectedQuote.Id);
                }
                window.ShowDialog();
                LoadQuotes(); // Refresh after close
            }
            catch (Exception ex)
            {
                StatusMessage = $"Teklif düzenlenemedi: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        private void DuplicateQuote()
        {
            if (SelectedQuote == null) return;

            var result = MessageBox.Show(
                $"'{SelectedQuote.Title}' teklifinin bir kopyası oluşturulacak. Devam edilsin mi?",
                "Teklifi Kopyala",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var year = DateTime.UtcNow.Year;
                var count = _context.ServiceProjects.Count(p => p.CreatedDate.Year == year) + 1;

                var copy = new ServiceProject
                {
                    Title = $"{SelectedQuote.Title} (Kopya)",
                    Name = SelectedQuote.Name,
                    CustomerId = SelectedQuote.CustomerId,
                    ProjectCode = $"PRJ-{year}-{count:D3}",
                    ProjectScopeJson = SelectedQuote.ProjectScopeJson,
                    TotalBudget = SelectedQuote.TotalBudget,
                    TotalCost = SelectedQuote.TotalCost,
                    TotalProfit = SelectedQuote.TotalProfit,
                    DiscountPercent = SelectedQuote.DiscountPercent,
                    CreatedDate = DateTime.UtcNow,
                    QuoteStatus = QuoteStatus.Draft,
                    RevisionNumber = 1,
                    KdvRate = SelectedQuote.KdvRate,
                    TotalUnitCount = SelectedQuote.TotalUnitCount,
                    SurveyNotes = SelectedQuote.SurveyNotes,
                    Notes = SelectedQuote.Notes,
                    PaymentTerms = SelectedQuote.PaymentTerms
                };

                // Generate quote number
                var quoteCount = _context.ServiceProjects.Count(p => p.QuoteNumber != null) + 1;
                copy.QuoteNumber = $"TEK-{year}-{quoteCount:D3}";

                _context.ServiceProjects.Add(copy);
                _context.SaveChanges();

                LoadQuotes();
                StatusMessage = $"Teklif kopyalandı: {copy.ProjectCode}";
                IsActionSuccessful = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Kopyalama hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        private void DeleteQuote()
        {
            if (SelectedQuote == null) return;

            var result = MessageBox.Show(
                $"'{SelectedQuote.Title}' teklifi kalıcı olarak silinecek.\n\nBu işlem geri alınamaz. Devam edilsin mi?",
                "Teklifi Sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                _context.ServiceProjects.Remove(SelectedQuote);
                _context.SaveChanges();

                LoadQuotes();
                StatusMessage = "Teklif silindi.";
                IsActionSuccessful = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Silme hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        private void ExportPdf()
        {
            if (SelectedQuote == null) return;

            try
            {
                var scopeNodes = ProjectScopeService.Deserialize(SelectedQuote.ProjectScopeJson);

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"Teklif_{SelectedQuote.Title}_{DateTime.UtcNow:yyyyMMdd}",
                    DefaultExt = ".pdf",
                    Filter = "PDF Belgeleri (.pdf)|*.pdf"
                };

                if (dialog.ShowDialog() != true) return;

                // Ensure customer is loaded
                if (SelectedQuote.Customer == null)
                {
                    SelectedQuote.Customer = _context.Customers.Find(SelectedQuote.CustomerId);
                }

                var pdfService = new PdfService();
                pdfService.GenerateProjectQuote(SelectedQuote, scopeNodes, dialog.FileName);

                var openResult = MessageBox.Show(
                    "PDF başarıyla oluşturuldu. Dosyayı açmak ister misiniz?",
                    "Başarılı",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (openResult == MessageBoxResult.Yes)
                {
                    new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true }
                    }.Start();
                }

                StatusMessage = "PDF oluşturuldu.";
                IsActionSuccessful = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"PDF hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        private void ChangeStatus(QuoteStatus newStatus)
        {
            if (SelectedQuote == null) return;

            var statusName = newStatus switch
            {
                QuoteStatus.Sent => "Gönderildi",
                QuoteStatus.Approved => "Onaylandı",
                QuoteStatus.Rejected => "Reddedildi",
                _ => newStatus.ToString()
            };

            string? rejectionReason = null;
            if (newStatus == QuoteStatus.Rejected)
            {
                rejectionReason = Microsoft.VisualBasic.Interaction.InputBox(
                    "Red nedeni (opsiyonel):", "Red Nedeni", "");
            }

            try
            {
                var dbProject = _context.ServiceProjects.Find(SelectedQuote.Id);
                if (dbProject == null) return;

                dbProject.QuoteStatus = newStatus;

                switch (newStatus)
                {
                    case QuoteStatus.Sent:
                        dbProject.SentDate = DateTime.UtcNow;
                        // Geçerlilik süresi: 30 gün
                        if (dbProject.ValidUntil == null)
                            dbProject.ValidUntil = DateTime.UtcNow.AddDays(30);
                        break;
                    case QuoteStatus.Approved:
                        dbProject.ApprovedDate = DateTime.UtcNow;
                        dbProject.PipelineStage = Shared.Enums.PipelineStage.Won;
                        break;
                    case QuoteStatus.Rejected:
                        dbProject.RejectedDate = DateTime.UtcNow;
                        dbProject.RejectionReason = rejectionReason;
                        dbProject.PipelineStage = Shared.Enums.PipelineStage.Lost;
                        break;
                }

                _context.SaveChanges();
                LoadQuotes();

                StatusMessage = $"Teklif durumu güncellendi: {statusName}";
                IsActionSuccessful = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Durum güncelleme hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        #endregion

        #region Helpers

        private void NotifyKpiChanged()
        {
            OnPropertyChanged(nameof(TotalQuoteCount));
            OnPropertyChanged(nameof(DraftCount));
            OnPropertyChanged(nameof(SentCount));
            OnPropertyChanged(nameof(ApprovedCount));
            OnPropertyChanged(nameof(RejectedCount));
            OnPropertyChanged(nameof(TotalApprovedAmount));
            OnPropertyChanged(nameof(TotalPendingAmount));
            OnPropertyChanged(nameof(TotalApprovedAmountDisplay));
            OnPropertyChanged(nameof(TotalPendingAmountDisplay));
        }

        /// <summary>
        /// Teklif durumuna göre renk döndürür
        /// </summary>
        public static string GetStatusColor(QuoteStatus status) => status switch
        {
            QuoteStatus.Draft => "#9E9E9E",
            QuoteStatus.Sent => "#2196F3",
            QuoteStatus.Approved => "#4CAF50",
            QuoteStatus.Rejected => "#F44336",
            QuoteStatus.Expired => "#FF9800",
            QuoteStatus.Revised => "#9C27B0",
            _ => "#757575"
        };

        /// <summary>
        /// Teklif durumunun Türkçe karşılığını döndürür
        /// </summary>
        public static string GetStatusText(QuoteStatus status) => status switch
        {
            QuoteStatus.Draft => "Taslak",
            QuoteStatus.Sent => "Gönderildi",
            QuoteStatus.Approved => "Onaylandı",
            QuoteStatus.Rejected => "Reddedildi",
            QuoteStatus.Expired => "Süresi Doldu",
            QuoteStatus.Revised => "Revize",
            _ => "Bilinmiyor"
        };

        #endregion
    }
}
