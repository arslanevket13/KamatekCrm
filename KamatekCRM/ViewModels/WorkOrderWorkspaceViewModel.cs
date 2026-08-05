using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;
using KamatekCrm.Services;
using KamatekCrm.Views;

namespace KamatekCrm.ViewModels
{
    public enum WorkOrderStageState
    {
        Pending,
        Current,
        Completed,
        Skipped
    }

    /// <summary>Süreç göstergesindeki tek aşama (Talep → Keşif → Teklif → Montaj → Teslim → Kapandı).</summary>
    public sealed class WorkOrderStageItem
    {
        public string Name { get; }
        public string Glyph { get; }
        public WorkOrderStageState State { get; set; }

        public WorkOrderStageItem(string name, string glyph)
        {
            Name = name;
            Glyph = glyph;
        }
    }

    /// <summary>
    /// İş Emri Çalışma Alanı: bir iş dosyasının tamamı tek ekranda.
    /// Süreç göstergesi + Genel Bakış / Keşif / Teklif / Montaj / Geçmiş sekmeleri.
    /// Aşama işlemleri, mevcut düzenleyicilere yönlendirilir (teklif editörü vb.).
    /// </summary>
    public partial class WorkOrderWorkspaceViewModel : ViewModelBase
    {
        private readonly int _jobId;
        private readonly ServiceJobRowDto _job;
        private readonly IServiceJobReadService _readService;
        private readonly IServiceJobCommandService _commandService;
        private readonly PdfService _pdfService;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;
        private readonly Func<Task>? _openGeneralEditor;

        private WorkOrderWorkflowDto? _workflow;

        public event Action? RequestClose;

        public int JobId => _jobId;
        public string JobNumber => $"#{_jobId:D6}";
        public string CustomerFullName => _job.CustomerFullName;
        public string CustomerPhone => _job.CustomerPhone;
        public string Description => _job.Description;
        public string WorkOrderTypeDisplay => _job.WorkOrderTypeDisplay;
        public string PriorityDisplay => _job.PriorityDisplay;
        public string StatusDisplay { get; private set; }
        public string AssignedTechnician => string.IsNullOrWhiteSpace(_job.AssignedTechnician) ? "Atanmadı" : _job.AssignedTechnician;
        public string ScheduledDateDisplay => _job.ScheduledDate?.ToString("dd.MM.yyyy HH:mm") ?? "Planlanmadı";
        public string CreatedDateDisplay => _job.CreatedDate.ToString("dd.MM.yyyy HH:mm");
        public string SlaStatusDisplay => _job.SlaStatusDisplay;

        public ObservableCollection<WorkOrderStageItem> Stages { get; } = new();
        public ObservableCollection<ServiceJobMaterialDto> Materials { get; } = new();
        public ObservableCollection<DiscoveryMaterialDto> DiscoveryMaterials { get; } = new();
        public ObservableCollection<DiscoveryVisitDto> DiscoveryVisits { get; } = new();
        public ObservableCollection<string> DiscoveryPhotos { get; } = new();
        public ObservableCollection<QuotationItemDto> QuotationItems { get; } = new();
        public ObservableCollection<InstallationMaterialDto> InstallationMaterials { get; } = new();
        public ObservableCollection<InstallationTaskDto> InstallationTasks { get; } = new();
        public ObservableCollection<ServiceJobHistoryDto> History { get; } = new();

        public string CurrentStageName { get; private set; } = "—";
        public string NextAction { get; private set; } = "—";
        public bool IsCancelled { get; private set; }
        public bool HasGeneralEditor => _openGeneralEditor != null;

        /// <summary>Pencere açılışında code-behind tarafından atanır; içeriden açılan editörlerin sahibi olur.</summary>
        public System.Windows.Window? OwnerWindow { get; set; }

        // ── Genel Bakış özeti ──
        public string MaterialSummary { get; private set; } = "—";
        public string MaterialTotalDisplay { get; private set; } = "—";
        public string QuoteSummaryDisplay { get; private set; } = "—";

        // ── Keşif ──
        public bool HasDiscovery { get; private set; }
        public string DiscoveryTechnician { get; private set; } = "—";
        public string DiscoveryLaborHours { get; private set; } = "—";
        public string DiscoveryTechnicalNotes { get; private set; } = "—";
        public string DiscoveryRecommendedSolution { get; private set; } = "—";
        public string DiscoveryMaterialSummary { get; private set; } = "—";
        public string DiscoveryStatusLine { get; private set; } = "Keşif kaydı henüz oluşturulmadı.";
        public string DiscoveryVisitSummary { get; private set; } = "Ziyaret kaydı yok";
        public string DiscoveryPhotoSummary { get; private set; } = "Fotoğraf yok";

        // ── Keşif tamamlama doğrulaması ──
        public bool DiscoveryReadyToComplete { get; private set; }
        public string DiscoveryCompletionSummary { get; private set; } = string.Empty;

        // ── Keşif komutları için erişim kısıtları ──
        public bool CanEditDiscovery { get; private set; }
        public string DiscoveryEditDisabledReason { get; private set; } = string.Empty;
        public bool CanConvertToQuote { get; private set; }
        public string ConvertToQuoteDisabledReason { get; private set; } = string.Empty;

        // ── Teklif ──
        public bool HasQuotation { get; private set; }
        public string QuotationNumberDisplay { get; private set; } = "—";
        public string QuotationRevisionDisplay { get; private set; } = "—";
        public string QuotationStatusDisplay { get; private set; } = "—";
        public string QuotationIssuedDisplay { get; private set; } = "—";
        public string QuotationValidUntilDisplay { get; private set; } = "—";
        public string QuotationSentDisplay { get; private set; } = "—";
        public string QuotationAcceptedDisplay { get; private set; } = "—";
        public string QuotationRejectedDisplay { get; private set; } = "—";
        public string QuotationRejectionReason { get; private set; } = "—";
        public string QuotationLaborDisplay { get; private set; } = "—";
        public string QuotationShippingDisplay { get; private set; } = "—";
        public string QuotationDiscountDisplay { get; private set; } = "—";
        public string QuotationTaxDisplay { get; private set; } = "—";
        public string QuotationTotalDisplay { get; private set; } = "—";
        public bool CanOpenQuotationEditor { get; private set; }
        public string QuotationEditorDisabledReason { get; private set; } = string.Empty;

        // ── Montaj ──
        public bool HasInstallation { get; private set; }
        public string InstallationTechnician { get; private set; } = "—";
        public string InstallationDateDisplay { get; private set; } = "—";
        public string InstallationNotes { get; private set; } = "—";
        public string InstallationLaborHoursDisplay { get; private set; } = "—";
        public string InstallationCompletedDisplay { get; private set; } = "—";
        public string InstallationCompletionTechnician { get; private set; } = "—";
        public string InstallationDeliveryNote { get; private set; } = "—";
        public string InstallationTaskSummary { get; private set; } = "—";
        public string InstallationMaterialSummary { get; private set; } = "—";
        public bool IsInstallationCompleted { get; private set; }

        // ── Montaj tamamlama doğrulaması ──
        public bool InstallationReadyToComplete { get; private set; }
        public string InstallationCompletionSummary { get; private set; } = string.Empty;

        // ── Montaj komutları için erişim kısıtları ──
        public bool CanEditInstallation { get; private set; }
        public string InstallationEditDisabledReason { get; private set; } = string.Empty;

        // ── Teslim (Paket 7) ──
        public bool HasDelivery { get; private set; }
        public bool IsDelivered { get; private set; }
        public string DeliveryDateDisplay { get; private set; } = "—";
        public string DeliveredByDisplay { get; private set; } = "—";
        public string DeliveryNoteDisplay { get; private set; } = "—";
        public string DeliverySignatureDisplay { get; private set; } = "İmza alınmadı";
        public string PaymentStatusDisplay { get; private set; } = "—";
        public string PaymentMethodDisplay { get; private set; } = "—";
        public string PaidAmountDisplay { get; private set; } = "—";
        public string DeliveryBalanceDisplay { get; private set; } = "—";
        public string InvoiceNumberDisplay { get; private set; } = "—";
        public string DeliveryStatusLine { get; private set; } = "Teslim kaydı henüz oluşturulmadı.";
        public bool CanOpenDeliveryEditor { get; private set; }
        public string DeliveryEditorDisabledReason { get; private set; } = string.Empty;
        public bool CanGenerateInvoice { get; private set; }
        public string InvoiceDisabledReason { get; private set; } = string.Empty;

        public WorkOrderWorkspaceViewModel(
            ServiceJobRowDto job,
            IServiceJobReadService readService,
            IServiceJobCommandService commandService,
            PdfService pdfService,
            IDialogService dialogService,
            IToastService toastService,
            Func<Task>? openGeneralEditor = null)
        {
            _job = job ?? throw new ArgumentNullException(nameof(job));
            _jobId = job.Id;
            _readService = readService;
            _commandService = commandService;
            _pdfService = pdfService;
            _dialogService = dialogService;
            _toastService = toastService;
            _openGeneralEditor = openGeneralEditor;
            StatusDisplay = ServiceJobRowDto.MapStatusDisplay(job.Status);

            Stages.Add(new WorkOrderStageItem("Talep", "📥"));
            Stages.Add(new WorkOrderStageItem("Keşif", "🔍"));
            Stages.Add(new WorkOrderStageItem("Teklif", "📄"));
            Stages.Add(new WorkOrderStageItem("Montaj", "🛠️"));
            Stages.Add(new WorkOrderStageItem("Teslim", "🚚"));
            Stages.Add(new WorkOrderStageItem("Kapandı", "✅"));
        }

        public async Task<bool> InitializeAsync()
        {
            var workflow = await _readService.GetWorkOrderWorkflowAsync(_jobId);
            if (workflow.IsFailure || workflow.Value is null)
            {
                _toastService.ShowError(workflow.Error ?? "İş emri verileri yüklenemedi.");
                return false;
            }

            _workflow = workflow.Value;
            LoadStages();
            await LoadOverviewAsync();
            LoadDiscovery();
            LoadQuotation();
            LoadInstallation();
            LoadDelivery();
            await LoadHistoryAsync();
            return true;
        }

        /// <summary>Keşif aşamasında yapılabilecek işlemlerin erişimini tazeler (durum değişince).</summary>
        private void RefreshDiscoveryAccess()
        {
            var status = _workflow?.JobStatus ?? _job.Status;
            bool converted = _workflow?.Quotation is not null || _job.QuotationId is not null;
            var access = ResolveDiscoveryAccess(status, converted);

            CanEditDiscovery = access.CanEdit;
            DiscoveryEditDisabledReason = access.EditDisabledReason;
            CanConvertToQuote = access.CanConvertToQuote;
            ConvertToQuoteDisabledReason = access.ConvertDisabledReason;

            OnPropertyChanged(nameof(CanEditDiscovery));
            OnPropertyChanged(nameof(DiscoveryEditDisabledReason));
            OnPropertyChanged(nameof(CanConvertToQuote));
            OnPropertyChanged(nameof(ConvertToQuoteDisabledReason));
        }

        /// <summary>Keşif düzenleme ve teklife dönüştürme erişimini duruma göre çözer (test edilebilir saf mantık).</summary>
        internal static (bool CanEdit, string EditDisabledReason, bool CanConvertToQuote, string ConvertDisabledReason)
            ResolveDiscoveryAccess(JobStatus status, bool converted)
        {
            // Pending de keşif akışına açıktır: yeni kayıt formu keşif işlerini DiscoveryRequest ile açar,
            // ancak eski kayıtlar Pending'de kalmış olabilir ve teklife dönüştürme guard'ı Pending'i kabul eder.
            bool discoveryPhase = status is JobStatus.DiscoveryRequest or JobStatus.PendingDiscovery
                or JobStatus.DiscoveryCompleted or JobStatus.Pending;

            string editReason = discoveryPhase
                ? string.Empty
                : status == JobStatus.ConvertedToQuote
                    ? "İş teklif aşamasına geçmiş; keşif raporu artık salt okunurdur."
                    : "Keşif raporu yalnızca keşif aşamasında düzenlenebilir.";

            string convertReason = converted
                ? "Bu iş emri zaten teklife dönüştürülmüş."
                : discoveryPhase
                    ? string.Empty
                    : "Keşiften teklife dönüştürme yalnızca keşif aşamasında yapılabilir.";

            return (discoveryPhase, editReason, discoveryPhase && !converted, convertReason);
        }

        // ── Süreç göstergesi ──

        internal static int MapStageIndex(JobStatus status) => status switch
        {
            JobStatus.Completed or JobStatus.Delivered => 5,
            JobStatus.InstallationCompleted => 4,
            JobStatus.InstallationPlanned => 3,
            JobStatus.ConvertedToQuote or JobStatus.Quoting or JobStatus.Rejected => 2,
            JobStatus.DiscoveryRequest or JobStatus.PendingDiscovery or JobStatus.DiscoveryCompleted => 1,
            _ => 0 // Talep: Pending, InProgress, WaitingForParts, WaitingForApproval
        };

        /// <summary>
        /// Süreç göstergesi, durum rozeti ve sıradaki işlem canlı durumdan türetilir
        /// (workflow yenilendikçe _workflow.JobStatus, aksi halde açılış anındaki satır).
        /// Etiket eşlemesi ServiceJobRowDto.MapStatusDisplay ortak yardımcısındadır.
        /// </summary>
        private void LoadStages()
        {
            var currentStatus = _workflow?.JobStatus ?? _job.Status;
            IsCancelled = currentStatus == JobStatus.Cancelled;
            int current = MapStageIndex(currentStatus);

            for (int i = 0; i < Stages.Count; i++)
            {
                Stages[i].State = IsCancelled
                    ? WorkOrderStageState.Skipped
                    : i < current ? WorkOrderStageState.Completed
                    : i == current ? WorkOrderStageState.Current
                    : WorkOrderStageState.Pending;
            }

            CurrentStageName = IsCancelled ? "İptal Edildi" : Stages[current].Name;
            NextAction = ResolveNextAction(currentStatus, _workflow);
            StatusDisplay = ServiceJobRowDto.MapStatusDisplay(currentStatus);
            OnPropertyChanged(nameof(IsCancelled));
            OnPropertyChanged(nameof(CurrentStageName));
            OnPropertyChanged(nameof(NextAction));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(HasGeneralEditor));
        }

        internal static string ResolveNextAction(JobStatus status, WorkOrderWorkflowDto? workflow)
        {
            if (status == JobStatus.Cancelled) return "İş iptal edildi; yeni ilerleme yapılamaz.";
            if (status == JobStatus.Delivered) return "İş teslim edildi ve kapandı — fatura / servis raporu PDF üretebilirsiniz.";
            if (status == JobStatus.InstallationCompleted) return "Montaj tamamlandı — işi teslim et ve ödemeyi tahsil et.";
            if (status == JobStatus.Completed) return "İş tamamlandı — teslim onayı ve arşivleme.";
            if (status == JobStatus.InstallationPlanned) return "Montajı uygula ve tamamla (teslim notu + stok tüketimi).";

            var quote = workflow?.Quotation;
            return status switch
            {
                JobStatus.DiscoveryRequest or JobStatus.PendingDiscovery => "Keşif randevusu planla",
                JobStatus.DiscoveryCompleted => "Teklif oluştur (keşfi teklife dönüştür)",
                JobStatus.ConvertedToQuote or JobStatus.Quoting when quote is not null => quote.Status switch
                {
                    QuotationStatus.Draft => "Teklifi tamamla ve müşteriye gönder",
                    QuotationStatus.Sent => "Müşteri cevabını bekle",
                    QuotationStatus.Accepted => "Montajı planla",
                    QuotationStatus.Rejected => "Revizyon oluştur veya işi kapat",
                    QuotationStatus.Expired => "Süresi doldu — yeni revizyon oluştur",
                    _ => "Teklif durumunu kontrol et"
                },
                JobStatus.ConvertedToQuote or JobStatus.Quoting => "Teklif oluştur",
                JobStatus.WaitingForApproval => "Müşteri onayını bekle",
                JobStatus.WaitingForParts => "Parça teminini bekle",
                _ => "İş akışını ilerlet"
            };
        }

        // ── Sekme yükleme ──

        private async Task LoadOverviewAsync()
        {
            try
            {
                Materials.Clear();
                var materials = await _readService.GetMaterialsAsync(_jobId);
                if (materials.IsSuccess && materials.Value is not null)
                {
                    foreach (var item in materials.Value) Materials.Add(item);
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Malzemeler yüklenemedi: {ex.Message}");
            }

            MaterialSummary = Materials.Count == 0 ? "Malzeme girilmedi" : $"{Materials.Count} kalem";
            MaterialTotalDisplay = $"{Materials.Sum(m => m.UnitPrice * m.QuantityUsed):N2} ₺";
            QuoteSummaryDisplay = _workflow?.Quotation is { } q
                ? $"{q.QuotationNumber} — {q.TotalAmount:N2} ₺ ({QuotationStatusName(q.Status)})"
                : "Teklif yok";

            OnPropertyChanged(nameof(MaterialSummary));
            OnPropertyChanged(nameof(MaterialTotalDisplay));
            OnPropertyChanged(nameof(QuoteSummaryDisplay));
        }

        private void LoadDiscovery()
        {
            DiscoveryMaterials.Clear();
            DiscoveryVisits.Clear();
            DiscoveryPhotos.Clear();

            var discovery = _workflow?.Discovery;
            HasDiscovery = discovery is not null;

            if (discovery is not null)
            {
                DiscoveryTechnician = string.IsNullOrWhiteSpace(discovery.TechnicianName) ? "Atanmadı" : discovery.TechnicianName;
                DiscoveryLaborHours = $"{discovery.EstimatedLaborHours:N1} saat";
                DiscoveryTechnicalNotes = string.IsNullOrWhiteSpace(discovery.TechnicalNotes) ? "Tespit notu girilmedi." : discovery.TechnicalNotes;
                DiscoveryRecommendedSolution = string.IsNullOrWhiteSpace(discovery.RecommendedSolution) ? "Çözüm önerisi girilmedi." : discovery.RecommendedSolution;
                DiscoveryMaterialSummary = discovery.Materials.Count == 0 ? "Malzeme öngörülmedi" : $"{discovery.Materials.Count} kalem";
                DiscoveryStatusLine = "Keşif raporu kaydedildi.";
                foreach (var material in discovery.Materials) DiscoveryMaterials.Add(material);
                foreach (var photo in discovery.PhotoPaths) DiscoveryPhotos.Add(photo);
            }
            else
            {
                DiscoveryTechnician = "—";
                DiscoveryLaborHours = "—";
                DiscoveryTechnicalNotes = "—";
                DiscoveryRecommendedSolution = "—";
                DiscoveryMaterialSummary = "—";
                DiscoveryStatusLine = "Keşif kaydı henüz oluşturulmadı.";
            }

            if (_workflow?.Visits is { } visits)
            {
                foreach (var visit in visits) DiscoveryVisits.Add(visit);
            }
            DiscoveryVisitSummary = DiscoveryVisits.Count == 0
                ? "Ziyaret kaydı yok"
                : $"{DiscoveryVisits.Count} ziyaret";
            DiscoveryPhotoSummary = DiscoveryPhotos.Count == 0
                ? "Fotoğraf yok"
                : $"{DiscoveryPhotos.Count} fotoğraf/belge";

            // Tamamlama doğrulaması: rapor + not/çözüm + malzeme veya ziyaret
            DiscoveryReadyToComplete =
                HasDiscovery &&
                (!string.IsNullOrWhiteSpace(discovery?.TechnicalNotes) || !string.IsNullOrWhiteSpace(discovery?.RecommendedSolution)) &&
                (DiscoveryMaterials.Count > 0 || DiscoveryVisits.Count > 0);
            DiscoveryCompletionSummary = DiscoveryReadyToComplete
                ? "Keşif tamamlanmaya hazır."
                : "Keşif tamamlanması için rapor, teknik not ve en az bir malzeme/ziyaret gerekli.";

            RefreshDiscoveryAccess();

            OnPropertyChanged(nameof(HasDiscovery));
            OnPropertyChanged(nameof(DiscoveryStatusLine));
            OnPropertyChanged(nameof(DiscoveryTechnician));
            OnPropertyChanged(nameof(DiscoveryLaborHours));
            OnPropertyChanged(nameof(DiscoveryTechnicalNotes));
            OnPropertyChanged(nameof(DiscoveryRecommendedSolution));
            OnPropertyChanged(nameof(DiscoveryMaterialSummary));
            OnPropertyChanged(nameof(DiscoveryVisitSummary));
            OnPropertyChanged(nameof(DiscoveryPhotoSummary));
            OnPropertyChanged(nameof(DiscoveryReadyToComplete));
            OnPropertyChanged(nameof(DiscoveryCompletionSummary));
        }

        private void LoadQuotation()
        {
            QuotationItems.Clear();
            var quote = _workflow?.Quotation;
            HasQuotation = quote is not null;
            if (quote is null)
            {
                QuotationEditorDisabledReason = "Bu iş emri için teklif oluşturulmamış; önce keşfi teklife dönüştürün.";
                OnPropertyChanged(nameof(QuotationEditorDisabledReason));
                return;
            }

            QuotationNumberDisplay = quote.QuotationNumber;
            QuotationRevisionDisplay = $"Revizyon {quote.RevisionNumber}";
            QuotationStatusDisplay = QuotationStatusName(quote.Status);
            QuotationIssuedDisplay = quote.IssuedDate.ToString("dd.MM.yyyy");
            QuotationValidUntilDisplay = quote.ValidUntil?.ToString("dd.MM.yyyy") ?? "—";
            QuotationSentDisplay = quote.SentDate?.ToString("dd.MM.yyyy HH:mm") ?? "Gönderilmedi";
            QuotationAcceptedDisplay = quote.AcceptedAt?.ToString("dd.MM.yyyy HH:mm") ?? "Kabul edilmedi";
            QuotationRejectedDisplay = quote.RejectedAt?.ToString("dd.MM.yyyy HH:mm") ?? "Reddedilmedi";
            QuotationRejectionReason = string.IsNullOrWhiteSpace(quote.RejectionReason) ? "—" : quote.RejectionReason;
            QuotationLaborDisplay = $"{quote.LaborCost:N2} ₺";
            QuotationShippingDisplay = $"{quote.ShippingCost:N2} ₺";
            QuotationDiscountDisplay = $"{quote.DiscountAmount:N2} ₺";
            QuotationTaxDisplay = $"{quote.TaxAmount:N2} ₺";
            QuotationTotalDisplay = $"{quote.TotalAmount:N2} ₺";
            foreach (var item in quote.Items) QuotationItems.Add(item);

            CanOpenQuotationEditor = quote.Status is QuotationStatus.Draft or QuotationStatus.Sent;
            QuotationEditorDisabledReason = quote.Status switch
            {
                QuotationStatus.Draft or QuotationStatus.Sent => "",
                QuotationStatus.Accepted => "Kabul edilmiş teklif doğrudan düzenlenemez; değişiklik için revizyon oluşturun.",
                QuotationStatus.Rejected => "Reddedilmiş teklif düzenlenemez; revizyon oluşturarak yeni teklif hazırlayın.",
                QuotationStatus.Cancelled => "İptal edilmiş teklif düzenlenemez.",
                QuotationStatus.Expired => "Süresi dolmuş teklif düzenlenemez; revizyon oluşturun.",
                _ => "Teklif bu durumda düzenlenemez."
            };
            OnPropertyChanged(nameof(HasQuotation));
            OnPropertyChanged(nameof(QuotationEditorDisabledReason));
            OnPropertyChanged(nameof(QuotationNumberDisplay));
            OnPropertyChanged(nameof(QuotationRevisionDisplay));
            OnPropertyChanged(nameof(QuotationStatusDisplay));
            OnPropertyChanged(nameof(QuotationIssuedDisplay));
            OnPropertyChanged(nameof(QuotationValidUntilDisplay));
            OnPropertyChanged(nameof(QuotationSentDisplay));
            OnPropertyChanged(nameof(QuotationAcceptedDisplay));
            OnPropertyChanged(nameof(QuotationRejectedDisplay));
            OnPropertyChanged(nameof(QuotationRejectionReason));
            OnPropertyChanged(nameof(QuotationLaborDisplay));
            OnPropertyChanged(nameof(QuotationShippingDisplay));
            OnPropertyChanged(nameof(QuotationDiscountDisplay));
            OnPropertyChanged(nameof(QuotationTaxDisplay));
            OnPropertyChanged(nameof(QuotationTotalDisplay));
            OnPropertyChanged(nameof(CanOpenQuotationEditor));
        }

        private void LoadInstallation()
        {
            InstallationMaterials.Clear();
            InstallationTasks.Clear();

            var installation = _workflow?.Installation;
            HasInstallation = installation is not null;
            IsInstallationCompleted = installation?.CompletedAt is not null;

            if (installation is null)
            {
                CanEditInstallation = false;
                InstallationEditDisabledReason = "Bu iş emri için montaj planlanmamış; önce teklif kabul edilip montaj planlanmalıdır.";
                InstallationReadyToComplete = false;
                InstallationCompletionSummary = "Montaj emri yok — önce montajı planlayın.";
                OnPropertyChanged(nameof(CanEditInstallation));
                OnPropertyChanged(nameof(InstallationEditDisabledReason));
                OnPropertyChanged(nameof(InstallationReadyToComplete));
                OnPropertyChanged(nameof(InstallationCompletionSummary));
                return;
            }

            InstallationTechnician = string.IsNullOrWhiteSpace(installation.TechnicianName) ? "Atanmadı" : installation.TechnicianName;
            InstallationDateDisplay = installation.InstallationDate?.ToString("dd.MM.yyyy HH:mm") ?? "Planlanmadı";
            InstallationNotes = string.IsNullOrWhiteSpace(installation.Notes) ? "Not girilmedi." : installation.Notes;
            InstallationLaborHoursDisplay = $"{installation.LaborHours:N1} saat";
            InstallationCompletedDisplay = installation.CompletedAt?.ToString("dd.MM.yyyy HH:mm") ?? "Tamamlanmadı";
            InstallationCompletionTechnician = string.IsNullOrWhiteSpace(installation.CompletionTechnician) ? "—" : installation.CompletionTechnician;
            InstallationDeliveryNote = string.IsNullOrWhiteSpace(installation.DeliveryNote) ? "—" : installation.DeliveryNote;
            InstallationTaskSummary = $"{installation.Tasks.Count(t => t.IsCompleted)}/{installation.Tasks.Count} görev tamam";
            InstallationMaterialSummary = installation.Materials.Count == 0 ? "Malzeme yok" : $"{installation.Materials.Count} kalem";

            foreach (var material in installation.Materials) InstallationMaterials.Add(material);
            foreach (var task in installation.Tasks) InstallationTasks.Add(task);

            // Tamamlama doğrulaması: en az bir malzeme + işçilik saati > 0
            InstallationReadyToComplete =
                !IsInstallationCompleted &&
                installation.Materials.Count > 0 &&
                installation.LaborHours > 0m;
            InstallationCompletionSummary = IsInstallationCompleted
                ? "Montaj tamamlandı — teslim formu PDF'e hazır."
                : InstallationReadyToComplete
                    ? "Montaj tamamlanmaya hazır (malzeme + işçilik saati)."
                    : "Montaj tamamlanması için en az bir malzeme ve işçilik saati gerekli.";

            CanEditInstallation = !IsInstallationCompleted;
            InstallationEditDisabledReason = IsInstallationCompleted
                ? "Montaj tamamlanmış; kayıt salt okunurdur."
                : string.Empty;

            OnPropertyChanged(nameof(HasInstallation));
            OnPropertyChanged(nameof(CanEditInstallation));
            OnPropertyChanged(nameof(IsInstallationCompleted));
            OnPropertyChanged(nameof(InstallationTechnician));
            OnPropertyChanged(nameof(InstallationDateDisplay));
            OnPropertyChanged(nameof(InstallationNotes));
            OnPropertyChanged(nameof(InstallationLaborHoursDisplay));
            OnPropertyChanged(nameof(InstallationCompletedDisplay));
            OnPropertyChanged(nameof(InstallationCompletionTechnician));
            OnPropertyChanged(nameof(InstallationDeliveryNote));
            OnPropertyChanged(nameof(InstallationTaskSummary));
            OnPropertyChanged(nameof(InstallationMaterialSummary));
            OnPropertyChanged(nameof(InstallationReadyToComplete));
            OnPropertyChanged(nameof(InstallationCompletionSummary));
            OnPropertyChanged(nameof(InstallationEditDisabledReason));
        }

        private void LoadDelivery()
        {
            var delivery = _workflow?.Delivery;
            var installation = _workflow?.Installation;
            var quote = _workflow?.Quotation;
            var status = _workflow?.JobStatus ?? _job.Status;

            HasDelivery = delivery is not null;
            IsDelivered = status == JobStatus.Delivered || delivery is not null;
            DeliveryStatusLine = IsDelivered
                ? "🚚 İş teslim edildi ve kapandı — fatura / servis raporu PDF üretebilirsiniz."
                : status == JobStatus.InstallationCompleted
                    ? "Montaj tamamlandı — teslim kaydını oluşturmak için 'Teslim Et' butonunu kullanın."
                    : "Teslim kaydı henüz oluşturulmadı.";

            if (delivery is not null)
            {
                DeliveryDateDisplay = delivery.DeliveryDate.ToString("dd.MM.yyyy HH:mm");
                DeliveredByDisplay = string.IsNullOrWhiteSpace(delivery.DeliveredBy) ? "—" : delivery.DeliveredBy;
                DeliveryNoteDisplay = string.IsNullOrWhiteSpace(delivery.DeliveryNote) ? "—" : delivery.DeliveryNote;
                DeliverySignatureDisplay = string.IsNullOrWhiteSpace(delivery.CustomerSignature)
                    ? "İmza alınmadı"
                    : "✍️ İmza alındı";
                PaymentStatusDisplay = PaymentStatusLabels.Map(delivery.PaymentStatus);
                PaymentMethodDisplay = PaymentMethodLabels.Map(delivery.PaymentMethod);
                PaidAmountDisplay = $"{delivery.PaidAmount:N2} ₺";
                InvoiceNumberDisplay = string.IsNullOrWhiteSpace(delivery.InvoiceNumber) ? "—" : delivery.InvoiceNumber;
            }
            else
            {
                DeliveryDateDisplay = "—";
                DeliveredByDisplay = "—";
                PaymentStatusDisplay = "—";
                PaymentMethodDisplay = "—";
                PaidAmountDisplay = "—";
                InvoiceNumberDisplay = "—";
                // Teslim notu / imza montaj tamamlama formundan gelir (ön bilgi)
                DeliveryNoteDisplay = string.IsNullOrWhiteSpace(installation?.DeliveryNote)
                    ? "—"
                    : installation.DeliveryNote;
                DeliverySignatureDisplay = string.IsNullOrWhiteSpace(installation?.CustomerSignature)
                    ? "İmza alınmadı"
                    : "✍️ İmza alındı (montaj formu)";
            }

            decimal total = quote?.TotalAmount ?? 0m;
            decimal paid = delivery?.PaidAmount ?? 0m;
            DeliveryBalanceDisplay = $"{Math.Max(0m, total - paid):N2} ₺";

            CanOpenDeliveryEditor = status is JobStatus.InstallationCompleted or JobStatus.Delivered;
            DeliveryEditorDisabledReason = status switch
            {
                JobStatus.InstallationCompleted => "",
                JobStatus.Delivered => "",
                JobStatus.InstallationPlanned => "Teslim için önce montajın tamamlanması gerekir.",
                _ => "Teslim yalnızca montaj tamamlandıktan sonra yapılabilir."
            };

            CanGenerateInvoice = quote is not null && quote.Status == QuotationStatus.Accepted;
            InvoiceDisabledReason = quote is null
                ? "Fatura için önce teklif oluşturulmalı."
                : quote.Status != QuotationStatus.Accepted
                    ? "Fatura için teklifin 'Kabul Edildi' durumunda olması gerekir."
                    : "";

            OnPropertyChanged(nameof(HasDelivery));
            OnPropertyChanged(nameof(IsDelivered));
            OnPropertyChanged(nameof(DeliveryStatusLine));
            OnPropertyChanged(nameof(DeliveryDateDisplay));
            OnPropertyChanged(nameof(DeliveredByDisplay));
            OnPropertyChanged(nameof(DeliveryNoteDisplay));
            OnPropertyChanged(nameof(DeliverySignatureDisplay));
            OnPropertyChanged(nameof(PaymentStatusDisplay));
            OnPropertyChanged(nameof(PaymentMethodDisplay));
            OnPropertyChanged(nameof(PaidAmountDisplay));
            OnPropertyChanged(nameof(DeliveryBalanceDisplay));
            OnPropertyChanged(nameof(InvoiceNumberDisplay));
            OnPropertyChanged(nameof(CanOpenDeliveryEditor));
            OnPropertyChanged(nameof(DeliveryEditorDisabledReason));
            OnPropertyChanged(nameof(CanGenerateInvoice));
            OnPropertyChanged(nameof(InvoiceDisabledReason));
        }

        private async Task LoadHistoryAsync()
        {
            History.Clear();
            var history = await _readService.GetHistoryAsync(_jobId);
            if (history.IsFailure || history.Value is null)
            {
                _toastService.ShowError(history.Error);
                return;
            }
            foreach (var entry in history.Value) History.Add(entry);
        }

        private static string QuotationStatusName(QuotationStatus status) => QuotationStatusLabels.Map(status);

        // ── Komutlar ──

        [RelayCommand]
        private async Task Refresh()
        {
            _toastService.ShowInfo("İş dosyası yenileniyor...");
            var ok = await InitializeAsync();
            if (ok) _toastService.ShowSuccess("İş dosyası güncellendi.");
        }

        [RelayCommand]
        private async Task EditDiscovery()
        {
            if (!CanEditDiscovery)
            {
                _toastService.ShowWarning(DiscoveryEditDisabledReason);
                return;
            }

            try
            {
                var vm = new DiscoveryEditorViewModel(
                    _jobId,
                    _readService,
                    _commandService,
                    _dialogService,
                    _toastService);

                if (!await vm.InitializeAsync())
                {
                    _toastService.ShowError("Keşif verileri yüklenemedi.");
                    return;
                }

                var window = new DiscoveryEditorWindow(vm)
                {
                    Owner = OwnerWindow ?? System.Windows.Application.Current.MainWindow
                };
                if (window.ShowDialog() == true)
                {
                    await InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Keşif ekranı açılamadı: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CompleteDiscovery()
        {
            if (!DiscoveryReadyToComplete)
            {
                _toastService.ShowWarning(DiscoveryCompletionSummary);
                return;
            }

            try
            {
                var result = await _commandService.CompleteDiscoveryAsync(
                    _jobId,
                    App.CurrentUser?.Username ?? "Sistem");
                if (result.IsFailure)
                {
                    _toastService.ShowError(result.Error);
                    await InitializeAsync();
                    return;
                }

                _toastService.ShowSuccess("Keşif tamamlandı. Şimdi teklif oluşturabilirsiniz.");
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Keşif tamamlanamadı: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ConvertToQuote()
        {
            if (!CanConvertToQuote)
            {
                _toastService.ShowWarning(ConvertToQuoteDisabledReason);
                return;
            }

            try
            {
                var conversion = await _commandService.ConvertToQuoteAsync(
                    _jobId,
                    App.CurrentUser?.Username ?? "Sistem");
                if (conversion.IsFailure)
                {
                    _toastService.ShowError(conversion.Error);
                    await InitializeAsync();
                    return;
                }

                _toastService.ShowSuccess($"İş #{_jobId} teklif aşamasına alındı; teklif kaydı oluşturuldu.");
                await InitializeAsync();

                if (await _dialogService.ShowConfirmationAsync(
                        "Teklif kaydı oluşturuldu. Fiyat ve şartları düzenlemek için teklif ekranını açmak ister misiniz?",
                        "Teklif Düzenle") && HasQuotation)
                {
                    await OpenQuotationEditor();
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Teklife dönüştürülürken hata: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task OpenQuotationEditor()
        {
            if (!HasQuotation)
            {
                _toastService.ShowWarning("Bu iş emri için teklif oluşturulmamış.");
                return;
            }

            try
            {
                var vm = new WorkOrderQuotationViewModel(
                    _jobId,
                    _readService,
                    _commandService,
                    _pdfService,
                    _dialogService,
                    _toastService);

                if (!await vm.InitializeAsync())
                {
                    _toastService.ShowError("Teklif verileri yüklenemedi.");
                    return;
                }

                var window = new WorkOrderQuotationWindow(vm)
                {
                    Owner = OwnerWindow ?? System.Windows.Application.Current.MainWindow
                };
                if (window.ShowDialog() == true)
                {
                    await InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Teklif ekranı açılamadı: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task OpenGeneralEditor()
        {
            if (_openGeneralEditor is not null)
            {
                await _openGeneralEditor();
                await InitializeAsync();
            }
        }

        // ── Montaj V2 ──

        [RelayCommand]
        private async Task EditInstallation()
        {
            if (!HasInstallation)
            {
                _toastService.ShowWarning(InstallationEditDisabledReason);
                return;
            }
            if (IsInstallationCompleted)
            {
                _toastService.ShowWarning("Montaj tamamlanmış; kayıt salt okunurdur.");
                return;
            }

            try
            {
                var vm = new InstallationEditorViewModel(
                    _jobId,
                    _readService,
                    _commandService,
                    _dialogService,
                    _toastService);

                if (!await vm.InitializeAsync())
                {
                    _toastService.ShowError("Montaj verileri yüklenemedi.");
                    return;
                }

                var window = new InstallationEditorWindow(vm)
                {
                    Owner = OwnerWindow ?? System.Windows.Application.Current.MainWindow
                };
                if (window.ShowDialog() == true)
                {
                    await InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Montaj ekranı açılamadı: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CompleteInstallation()
        {
            if (!InstallationReadyToComplete)
            {
                _toastService.ShowWarning(InstallationCompletionSummary);
                return;
            }

            try
            {
                var vm = new InstallationEditorViewModel(
                    _jobId,
                    _readService,
                    _commandService,
                    _dialogService,
                    _toastService);
                if (!await vm.InitializeAsync())
                {
                    _toastService.ShowError("Montaj verileri yüklenemedi.");
                    return;
                }

                var window = new InstallationEditorWindow(vm)
                {
                    Owner = OwnerWindow ?? System.Windows.Application.Current.MainWindow
                };
                if (window.ShowDialog() == true)
                {
                    await InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Montaj ekranı açılamadı: {ex.Message}");
            }
        }

        // ── Teslim V2 (Paket 7) ──

        [RelayCommand]
        private async Task OpenDeliveryEditor()
        {
            if (!CanOpenDeliveryEditor)
            {
                _toastService.ShowWarning(DeliveryEditorDisabledReason);
                return;
            }

            try
            {
                var vm = new DeliveryEditorViewModel(
                    _jobId,
                    _readService,
                    _commandService,
                    _toastService);

                if (!await vm.InitializeAsync())
                {
                    _toastService.ShowError("Teslim verileri yüklenemedi.");
                    return;
                }

                var window = new DeliveryEditorWindow(vm)
                {
                    Owner = OwnerWindow ?? System.Windows.Application.Current.MainWindow
                };
                if (window.ShowDialog() == true)
                {
                    await InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Teslim ekranı açılamadı: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task GenerateInvoicePdf()
        {
            if (!CanGenerateInvoice || _workflow?.Quotation is null)
            {
                _toastService.ShowWarning(InvoiceDisabledReason);
                return;
            }

            try
            {
                var document = await _readService.GetDocumentAsync(_jobId);
                if (document.IsFailure || document.Value is null)
                {
                    _toastService.ShowError(document.Error);
                    return;
                }

                var filePath = await _dialogService.ShowSaveFileDialogAsync(
                    "Faturayı Kaydet",
                    "PDF Dosyası (*.pdf)|*.pdf",
                    $"fatura_is_{_jobId:D6}.pdf");
                if (string.IsNullOrWhiteSpace(filePath)) return;

                _pdfService.GenerateWorkOrderInvoice(_workflow, document.Value, filePath);
                _toastService.ShowSuccess("Fatura PDF üretildi.");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Fatura üretilemedi: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task GenerateServiceReportPdf()
        {
            if (_workflow is null) return;

            try
            {
                var document = await _readService.GetDocumentAsync(_jobId);
                if (document.IsFailure || document.Value is null)
                {
                    _toastService.ShowError(document.Error);
                    return;
                }

                var filePath = await _dialogService.ShowSaveFileDialogAsync(
                    "Servis Raporunu Kaydet",
                    "PDF Dosyası (*.pdf)|*.pdf",
                    $"servis_raporu_{_jobId:D6}.pdf");
                if (string.IsNullOrWhiteSpace(filePath)) return;

                _pdfService.GenerateWorkOrderServiceReport(_workflow, document.Value, filePath);
                _toastService.ShowSuccess("Servis raporu PDF üretildi.");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Servis raporu üretilemedi: {ex.Message}");
            }
        }

        [RelayCommand]
        private void Close() => RequestClose?.Invoke();
    }
}
