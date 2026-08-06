using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>Belgeler sekmesindeki tek PDF türü (tür, açıklama, hazır olma durumu ve üretim komutu).</summary>
    public sealed class WorkspaceDocumentItem
    {
        public string Key { get; }
        public string Title { get; }
        public string Glyph { get; }
        public string Description { get; }
        public bool IsAvailable { get; }
        public string UnavailableReason { get; }
        public IAsyncRelayCommand Command { get; }

        public WorkspaceDocumentItem(
            string key,
            string title,
            string glyph,
            string description,
            bool isAvailable,
            string unavailableReason,
            Func<WorkspaceDocumentItem, Task> generate)
        {
            Key = key;
            Title = title;
            Glyph = glyph;
            Description = description;
            IsAvailable = isAvailable;
            UnavailableReason = unavailableReason;
            Command = new AsyncRelayCommand(() => generate(this));
        }
    }

    /// <summary>Belgeler sekmesindeki tek fotoğraf/dosya (yol + kaynak etiketi + açma komutu).</summary>
    public sealed class WorkspacePhotoItem
    {
        public string FilePath { get; }
        public string Source { get; }
        public IAsyncRelayCommand OpenCommand { get; }

        public WorkspacePhotoItem(string filePath, string source, Action<string> showError)
        {
            FilePath = filePath;
            Source = source;
            OpenCommand = new AsyncRelayCommand(() =>
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        showError("Dosya bulunamadı: " + filePath);
                        return Task.CompletedTask;
                    }
                    Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    showError($"Dosya açılamadı: {ex.Message}");
                }
                return Task.CompletedTask;
            });
        }
    }

    /// <summary>
    /// Belgeler sekmesi: iş dosyasının üretilebilir PDF'lerini (keşif raporu, teklif, montaj
    /// emri, tamamlama formu, fatura, servis raporu) tek listede toplar ve keşif/ziyaret
    /// fotoğraflarını gösterir. Hazır olma durumu iş akışı verisinden türetilir; UI kural
    /// üretmez (fatura ve servis raporu kuralları resolver ile aynı kaynaktan gelir).
    /// </summary>
    public partial class WorkspaceDocumentsViewModel : WorkspaceTabViewModelBase
    {
        private readonly int _jobId;
        private readonly IServiceJobReadService _readService;
        private readonly PdfService _pdfService;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;
        private readonly Func<WorkOrderWorkflowDto?> _workflowProvider;

        public WorkspaceDocumentsViewModel(
            int jobId,
            IServiceJobReadService readService,
            PdfService pdfService,
            IDialogService dialogService,
            IToastService toastService,
            Func<WorkOrderWorkflowDto?> workflowProvider)
            : base(null) // Bu sekme ActionItem değil, kendi belge listesini kullanır.
        {
            _jobId = jobId;
            _readService = readService;
            _pdfService = pdfService;
            _dialogService = dialogService;
            _toastService = toastService;
            _workflowProvider = workflowProvider;
        }

        public ObservableCollection<WorkspaceDocumentItem> Documents { get; } = new();
        public ObservableCollection<WorkspacePhotoItem> Photos { get; } = new();
        public bool HasPhotos => Photos.Count > 0;

        protected override bool IsRelevantAction(WorkOrderAction action) => false;

        /// <summary>Workspace projeksiyonundan belge hazır olma durumlarını ve fotoğrafları kurar.</summary>
        public void ApplyData(WorkOrderWorkspaceDto dto)
        {
            var workflow = _workflowProvider() ?? new WorkOrderWorkflowDto(dto.JobId, dto.JobStatus, dto.DiscoverySummary, dto.QuotationSummary, dto.InstallationSummary, dto.Visits, dto.DeliverySummary);

            Documents.Clear();
            Documents.Add(new WorkspaceDocumentItem(
                "Discovery", "Keşif Raporu", "🔍",
                "Teknik tespitler, önerilen çözüm, tahmini malzemeler ve ziyaretlerin bulunduğu keşif raporu PDF'i.",
                IsAvailable("Discovery", workflow), "Bu iş için keşif raporu yok.", GenerateAsync));

            Documents.Add(new WorkspaceDocumentItem(
                "Quotation", "Fiyat Teklifi", "📄",
                "Teklif kalemleri, satır bazlı KDV, işçilik, nakliye ve ticari şartları içeren teklif PDF'i.",
                IsAvailable("Quotation", workflow), "Bu iş için teklif oluşturulmadı.", GenerateAsync));

            Documents.Add(new WorkspaceDocumentItem(
                "Installation", "Montaj Emri", "🛠️",
                "Montaj tarihi, teknisyen, malzemeler ve görev listesini içeren montaj emri PDF'i.",
                IsAvailable("Installation", workflow), "Montaj henüz planlanmadı.", GenerateAsync));

            Documents.Add(new WorkspaceDocumentItem(
                "CompletionForm", "Montaj Tamamlama Formu", "✅",
                "Fiili işçilik saati, teslim notu, tamamlayan teknisyen ve müşteri imzasını içeren form.",
                IsAvailable("CompletionForm", workflow), "Montaj tamamlandığında üretilebilir.", GenerateAsync));

            Documents.Add(new WorkspaceDocumentItem(
                "Invoice", "Fatura", "🧾",
                "Kabul edilmiş teklif kalemleri, işçilik, nakliye ve KDV üzerinden fatura PDF'i.",
                IsAvailable("Invoice", workflow),
                IsAvailable("Invoice", workflow) ? string.Empty : "Fatura için teklifin kabul edilmiş olması (veya teslim kaydı) gerekir.",
                GenerateAsync));

            Documents.Add(new WorkspaceDocumentItem(
                "ServiceReport", "Servis Raporu", "📋",
                "İş künyesi, keşif, montaj ve teslim özetini birleştiren servis raporu PDF'i.",
                IsAvailable("ServiceReport", workflow), "İptal edilmiş iş için servis raporu üretilemez.", GenerateAsync));

            OnPropertyChanged(nameof(Documents));

            Photos.Clear();
            if (workflow.Discovery is { PhotoPaths.Count: > 0 } discovery)
            {
                foreach (var path in discovery.PhotoPaths)
                {
                    Photos.Add(new WorkspacePhotoItem(path, "Keşif raporu", msg => _toastService.ShowError(msg)));
                }
            }
            if (workflow.Visits is not null)
            {
                foreach (var visit in workflow.Visits)
                {
                    foreach (var path in visit.PhotoPaths)
                    {
                        Photos.Add(new WorkspacePhotoItem(path, $"Ziyaret · {visit.VisitDate:dd.MM.yyyy}", msg => _toastService.ShowError(msg)));
                    }
                }
            }
            OnPropertyChanged(nameof(Photos));
            OnPropertyChanged(nameof(HasPhotos));
        }

        /// <summary>Belge hazır olma kuralı — tek kaynak (ApplyData ve GenerateAsync aynı kuralı kullanır).</summary>
        private static bool IsAvailable(string key, WorkOrderWorkflowDto workflow) => key switch
        {
            "Discovery" => workflow.Discovery is not null,
            "Quotation" => workflow.Quotation is not null,
            "Installation" => workflow.Installation is not null,
            "CompletionForm" => workflow.Installation?.CompletedAt is not null,
            "Invoice" => workflow.Quotation?.Status == QuotationStatus.Accepted || workflow.Delivery is not null,
            "ServiceReport" => workflow.JobStatus != JobStatus.Cancelled,
            _ => false
        };

        /// <summary>Tek bir belgeyi üretir: kaydetme dialogu → PDF üretimi → başarı bildirimi.</summary>
        public async Task GenerateAsync(WorkspaceDocumentItem item)
        {
            var workflow = _workflowProvider();
            if (workflow is null)
            {
                _toastService.ShowError("İş emri verileri yüklenemedi; sayfayı yenileyin.");
                return;
            }

            // Hazırlık, güncel workflow üzerinden yeniden doğrulanır — listedeki bayat duruma güvenilmez.
            if (!IsAvailable(item.Key, workflow))
            {
                _toastService.ShowWarning(item.UnavailableReason);
                return;
            }

            var document = await _readService.GetDocumentAsync(_jobId);
            if (document.IsFailure || document.Value is null)
            {
                _toastService.ShowError(document.Error);
                return;
            }

            try
            {
                var filePath = await _dialogService.ShowSaveFileDialogAsync(
                    $"{item.Title} — Kaydet", "PDF Dosyası (*.pdf)|*.pdf",
                    $"{item.Key.ToLowerInvariant()}_{_jobId:D6}.pdf");
                if (string.IsNullOrWhiteSpace(filePath)) return;

                switch (item.Key)
                {
                    case "Discovery":
                        _pdfService.GenerateDiscoveryReportPdf(workflow.Discovery!, document.Value, filePath);
                        break;
                    case "Quotation":
                        _pdfService.GenerateWorkOrderQuotationPdf(workflow.Quotation!, document.Value, filePath);
                        break;
                    case "Installation":
                        _pdfService.GenerateInstallationOrderPdf(workflow.Installation!, document.Value, filePath);
                        break;
                    case "CompletionForm":
                        _pdfService.GenerateInstallationCompletionFormPdf(workflow.Installation!, document.Value, filePath);
                        break;
                    case "Invoice":
                        _pdfService.GenerateWorkOrderInvoice(workflow, document.Value, filePath);
                        break;
                    case "ServiceReport":
                        _pdfService.GenerateWorkOrderServiceReport(workflow, document.Value, filePath);
                        break;
                    default:
                        _toastService.ShowError("Bilinmeyen belge türü.");
                        return;
                }

                _toastService.ShowSuccess($"{item.Title} PDF oluşturuldu.");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"{item.Title} üretilemedi: {ex.Message}");
            }
        }
    }
}
