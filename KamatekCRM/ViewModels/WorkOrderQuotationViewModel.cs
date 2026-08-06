using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
    /// <summary>
    /// Teklif kalemi satırı — miktar/fiyat/iskonto/KDV düzenlenebilir, satır bazlı
    /// net, KDV ve toplam tutarlar anında hesaplanır.
    /// </summary>
    public sealed class QuotationItemRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Kayıtlı satırın DB kimliği; yeni satırlarda null.</summary>
        public int? SourceId { get; }

        private int? _productId;
        public int? ProductId { get => _productId; set { _productId = value; OnChanged(); } }

        private string _productName;
        public string ProductName { get => _productName; set { _productName = value; OnChanged(); } }

        private decimal _quantity;
        public decimal Quantity
        {
            get => _quantity;
            set { _quantity = Math.Max(0m, value); OnChanged(); }
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set { _unitPrice = Math.Max(0m, value); OnChanged(); }
        }

        private decimal _discountPercent;
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set { _discountPercent = Math.Max(0m, value); OnChanged(); }
        }

        private decimal _taxPercent;
        public decimal TaxPercent
        {
            get => _taxPercent;
            set { _taxPercent = Math.Max(0m, value); OnChanged(); }
        }

        /// <summary>Net satır toplamı (iskonto uygulanmış, KDV hariç).</summary>
        public decimal LineNet => Math.Round(Quantity * UnitPrice * (1m - DiscountPercent / 100m), 2);

        /// <summary>Satır KDV tutarı (satırın kendi oranıyla).</summary>
        public decimal LineTax => Math.Round(LineNet * TaxPercent / 100m, 2);

        /// <summary>KDV dahil satır toplamı.</summary>
        public decimal LineTotalWithTax => Math.Round(LineNet + LineTax, 2);

        public QuotationItemRow(QuotationItemDto item)
            : this(item.Id, item.ProductId, item.ProductName, item.Quantity,
                   item.UnitPrice, item.DiscountPercent, item.TaxPercent)
        {
        }

        public QuotationItemRow(
            int? sourceId,
            int? productId,
            string productName,
            decimal quantity,
            decimal unitPrice,
            decimal discountPercent,
            decimal taxPercent)
        {
            SourceId = sourceId;
            ProductId = productId;
            _productName = productName;
            _quantity = quantity;
            _unitPrice = unitPrice;
            _discountPercent = discountPercent;
            _taxPercent = taxPercent;
        }

        private void OnChanged() => PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>
    /// İş emri teklif düzenleme ekranı: malzeme ekleme (stok/özel/hizmet), satır
    /// silme/çoğaltma/sıralama, satır bazlı KDV, işçilik, nakliye, açıklamalar,
    /// garanti, teslim süresi ve ödeme şartları.
    /// </summary>
    public partial class WorkOrderQuotationViewModel : ViewModelBase
    {
        private readonly int _jobId;
        private readonly IServiceJobReadService _readService;
        private readonly IServiceJobCommandService _commandService;
        private readonly PdfService _pdfService;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;

        private WorkOrderQuotationDto _quotation = null!;
        private ServiceJobDocumentDto _jobDocument = null!;

        public event Action? RequestClose;
        public event Action? RequestCloseWithSuccess;

        public ObservableCollection<QuotationItemRow> Items { get; } = new();

        public string QuotationNumber { get; private set; } = string.Empty;
        public string StatusDisplay { get; private set; } = string.Empty;
        public string RevisionDisplay { get; private set; } = "Revizyon 0";
        public bool IsEditable { get; private set; } = true;

        /// <summary>Taslak teklif doğrudan düzenlenebildiği için revizyon yalnızca diğer durumlarda açılır.</summary>
        public bool CanCreateRevision { get; private set; }

        /// <summary>"Teklifi Gönder" yalnızca Taslak teklifte anlamlıdır (gönderildi → durum değişir).</summary>
        public bool CanSendQuotation => _quotation is not null && IsEditable && _quotation.Status == QuotationStatus.Draft;

        /// <summary>Kabul edilmiş/reddedilmiş teklif veya geçmiş revizyon görüntülenirken salt okunur mod.</summary>
        public bool IsViewMode { get; private set; }

        /// <summary>Geçmiş bir revizyon görüntülenirken true (güncel kayıt değil).</summary>
        public bool IsViewingHistoricRevision { get; private set; }
        public string ViewingRevisionDisplay { get; private set; } = string.Empty;
        public string ViewModeBannerText { get; private set; } = string.Empty;

        public ObservableCollection<QuotationRevisionSummaryDto> Revisions { get; } = new();
        public QuotationRevisionSummaryDto? SelectedRevision { get; set; }

        private int _currentQuotationId;

        private string _description = string.Empty;
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private string _warranty = string.Empty;
        public string Warranty { get => _warranty; set => SetProperty(ref _warranty, value); }

        private string _deliveryTime = string.Empty;
        public string DeliveryTime { get => _deliveryTime; set => SetProperty(ref _deliveryTime, value); }

        private string _paymentTerms = string.Empty;
        public string PaymentTerms { get => _paymentTerms; set => SetProperty(ref _paymentTerms, value); }

        private decimal _laborCost;
        public decimal LaborCost { get => _laborCost; set { SetProperty(ref _laborCost, value); RecalculateTotals(); } }

        private decimal _shippingCost;
        public decimal ShippingCost { get => _shippingCost; set { SetProperty(ref _shippingCost, value); RecalculateTotals(); } }

        private decimal _discountAmount;
        public decimal DiscountAmount { get => _discountAmount; set { SetProperty(ref _discountAmount, value); RecalculateTotals(); } }

        /// <summary>Yeni eklenen satırlar için varsayılan KDV oranı (%).</summary>
        private decimal _taxRate = 20m;
        public decimal TaxRate { get => _taxRate; set { SetProperty(ref _taxRate, value); RecalculateTotals(); } }

        /// <summary>Malzeme/hizmet net toplamı (satır bazlı iskonto uygulanmış, KDV hariç).</summary>
        public decimal Subtotal => Items.Sum(i => i.LineNet);
        public decimal NetTotal => Subtotal - DiscountAmount + LaborCost + ShippingCost;

        /// <summary>KDV tutarı — satır bazlı oranların toplamı (servisle aynı yuvarlama).</summary>
        public decimal TaxAmount => Math.Round(Items.Sum(i => i.LineTax), 2);
        public decimal TotalAmount => Math.Round(NetTotal + TaxAmount, 2);

        public WorkOrderQuotationViewModel(
            int jobId,
            IServiceJobReadService readService,
            IServiceJobCommandService commandService,
            PdfService pdfService,
            IDialogService dialogService,
            IToastService toastService)
        {
            _jobId = jobId;
            _readService = readService;
            _commandService = commandService;
            _pdfService = pdfService;
            _dialogService = dialogService;
            _toastService = toastService;

            Items.CollectionChanged += (_, _) => RecalculateTotals();
        }

        public async Task<bool> InitializeAsync()
        {
            var workflow = await _readService.GetWorkOrderWorkflowAsync(_jobId);
            if (workflow.IsFailure || workflow.Value is null || workflow.Value.Quotation is null)
            {
                _toastService.ShowError(workflow.Error ?? "Bu iş emri için teklif bulunamadı.");
                return false;
            }

            var document = await _readService.GetDocumentAsync(_jobId);
            if (document.IsFailure || document.Value is null)
            {
                _toastService.ShowError(document.Error);
                return false;
            }

            _quotation = workflow.Value.Quotation;
            _jobDocument = document.Value;
            _currentQuotationId = _quotation.Id;

            var revisions = await _readService.GetQuotationRevisionsAsync(_jobId);
            if (revisions.IsSuccess && revisions.Value is not null)
            {
                Revisions.Clear();
                foreach (var revision in revisions.Value) Revisions.Add(revision);
            }
            else if (revisions.IsFailure)
            {
                _toastService.ShowError(revisions.Error);
            }

            IsEditable = _quotation.Status is QuotationStatus.Draft or QuotationStatus.Sent;
            CanCreateRevision = _quotation.Status != QuotationStatus.Draft;
            IsViewMode = !IsEditable;
            IsViewingHistoricRevision = false;
            ViewingRevisionDisplay = string.Empty;
            UpdateBanner();

            ApplyQuotation(_quotation);
            return true;
        }

        /// <summary>Ekranda gösterilecek teklifi alanlara uygular (başlangıç, revizyon görüntüleme, güncele dönüş).</summary>
        private void ApplyQuotation(WorkOrderQuotationDto quotation)
        {
            _quotation = quotation;

            QuotationNumber = quotation.QuotationNumber;
            RevisionDisplay = $"Revizyon {quotation.RevisionNumber}";
            StatusDisplay = QuotationStatusLabels.Map(quotation.Status);

            Description = quotation.Description ?? string.Empty;
            Warranty = quotation.Warranty ?? string.Empty;
            DeliveryTime = quotation.DeliveryTime ?? string.Empty;
            PaymentTerms = quotation.PaymentTerms ?? string.Empty;
            LaborCost = quotation.LaborCost;
            ShippingCost = quotation.ShippingCost;
            DiscountAmount = quotation.DiscountAmount;
            TaxRate = quotation.TaxRate;

            Items.Clear();
            foreach (var item in quotation.Items)
            {
                Items.Add(new QuotationItemRow(item));
            }

            OnPropertyChanged(nameof(IsEditable));
            OnPropertyChanged(nameof(CanCreateRevision));
            OnPropertyChanged(nameof(CanSendQuotation));
            OnPropertyChanged(nameof(IsViewMode));
            OnPropertyChanged(nameof(IsViewingHistoricRevision));
            OnPropertyChanged(nameof(ViewingRevisionDisplay));
            OnPropertyChanged(nameof(ViewModeBannerText));
            OnPropertyChanged(nameof(QuotationNumber));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(RevisionDisplay));
            RecalculateTotals();
        }

        private void UpdateBanner()
        {
            ViewModeBannerText = IsViewingHistoricRevision
                ? "🕓 Geçmiş revizyon görüntüleniyor (salt okunur). Güncel kayda dönmek için 'Güncel Revizyona Dön' kullanın."
                : IsViewMode
                    ? "🔒 Görüntüleme modu — bu teklif kabul edilmiş/reddedilmiş durumda; değişiklik için yeni revizyon oluşturun."
                    : string.Empty;
            OnPropertyChanged(nameof(ViewModeBannerText));
        }

        private void RecalculateTotals()
        {
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(NetTotal));
            OnPropertyChanged(nameof(TaxAmount));
            OnPropertyChanged(nameof(TotalAmount));
            SaveCommand.NotifyCanExecuteChanged();
        }

        private bool CanSave() => IsEditable && Items.Any(i => i.Quantity > 0);

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task Save()
        {
            if (!IsEditable)
            {
                _toastService.ShowWarning("Bu teklif düzenlenemez; durum kilidi aktif.");
                return;
            }

            var request = new UpdateWorkOrderQuotationRequest(
                _quotation.Id,
                Description,
                Warranty,
                DeliveryTime,
                PaymentTerms,
                LaborCost,
                ShippingCost,
                DiscountAmount,
                TaxRate,
                Items.Select((i, index) => new QuotationItemInput(
                    i.SourceId, i.ProductId, i.ProductName, i.Quantity,
                    i.UnitPrice, i.DiscountPercent, i.TaxPercent, index)).ToList());

            var result = await _commandService.UpdateQuotationAsync(request);
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            _toastService.ShowSuccess($"Teklif güncellendi — Genel Toplam: {result.Value!.TotalAmount:N2} ₺");
            RequestCloseWithSuccess?.Invoke();
        }

        // ── Satır yönetimi ──

        [RelayCommand]
        private async Task AddStockItem()
        {
            if (!IsEditable)
            {
                _toastService.ShowWarning("Bu teklif düzenlenemez; durum kilidi aktif.");
                return;
            }

            var picker = new ProductPickerWindow(async term =>
            {
                var result = await _readService.SearchProductsAsync(term);
                return result.IsSuccess && result.Value is not null ? result.Value : [];
            });
            picker.Owner = Application.Current?.MainWindow;
            if (picker.ShowDialog() != true || picker.SelectedProduct is null) return;

            var product = picker.SelectedProduct;
            AddRow(
                product.Id,
                product.ProductName,
                product.SalePrice,
                product.StockQuantity);
        }

        [RelayCommand]
        private async Task AddCustomItem()
        {
            if (!IsEditable)
            {
                _toastService.ShowWarning("Bu teklif düzenlenemez; durum kilidi aktif.");
                return;
            }
            string? name = await _dialogService.ShowInputAsync("Özel malzeme adı:", "Özel Malzeme Ekle");
            if (string.IsNullOrWhiteSpace(name)) return;
            AddRow(null, name.Trim(), 0m, null);
        }

        [RelayCommand]
        private async Task AddServiceItem()
        {
            if (!IsEditable)
            {
                _toastService.ShowWarning("Bu teklif düzenlenemez; durum kilidi aktif.");
                return;
            }
            string? name = await _dialogService.ShowInputAsync(
                "Hizmet / işçilik açıklaması:", "Hizmet ve İşçilik Ekle", "İşçilik");
            if (string.IsNullOrWhiteSpace(name)) return;
            AddRow(null, name.Trim(), 0m, null);
        }

        [RelayCommand]
        private void RemoveItem(QuotationItemRow? row)
        {
            if (!IsEditable || row is null) return;
            Items.Remove(row);
            RecalculateTotals();
        }

        [RelayCommand]
        private void DuplicateItem(QuotationItemRow? row)
        {
            if (!IsEditable || row is null) return;
            var copy = new QuotationItemRow(
                null, row.ProductId, row.ProductName, row.Quantity,
                row.UnitPrice, row.DiscountPercent, row.TaxPercent);
            int index = Items.IndexOf(row);
            Items.Insert(index + 1, copy);
            RecalculateTotals();
        }

        [RelayCommand]
        private void MoveItemUp(QuotationItemRow? row)
        {
            if (!IsEditable || row is null) return;
            int index = Items.IndexOf(row);
            if (index <= 0) return;
            Items.Move(index, index - 1);
        }

        [RelayCommand]
        private void MoveItemDown(QuotationItemRow? row)
        {
            if (!IsEditable || row is null) return;
            int index = Items.IndexOf(row);
            if (index < 0 || index >= Items.Count - 1) return;
            Items.Move(index, index + 1);
        }

        private void AddRow(int? productId, string name, decimal unitPrice, int? stockQuantity)
        {
            if (!IsEditable) return;

            var row = new QuotationItemRow(
                null, productId, name, 1, unitPrice, 0m, TaxRate);
            Items.Add(row);

            _toastService.ShowInfo(
                productId.HasValue && stockQuantity is { } stock
                    ? $"'{name}' teklife eklendi (stok: {stock})."
                    : $"'{name}' satırı eklendi.");
            RecalculateTotals();
        }

        [RelayCommand]
        private async Task CreateRevision()
        {
            if (IsViewingHistoricRevision)
            {
                _toastService.ShowWarning("Geçmiş revizyon üzerinden revizyon oluşturulamaz; önce güncel teklife dönün.");
                return;
            }
            if (_quotation.Status == QuotationStatus.Draft)
            {
                _toastService.ShowInfo("Taslak teklif doğrudan düzenlenebilir; revizyon oluşturmaya gerek yok.");
                return;
            }

            var result = await _commandService.CreateRevisionAsync(_quotation.Id, "Kullanıcı");
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            _toastService.ShowSuccess($"Revizyon {result.Value.RevisionNumber} oluşturuldu; düzenlemeye hazır.");
            await InitializeAsync();
        }

        [RelayCommand]
        private async Task SendQuotation()
        {
            if (IsViewingHistoricRevision)
            {
                _toastService.ShowWarning("Geçmiş revizyon üzerinde işlem yapılamaz; önce güncel teklife dönün.");
                return;
            }
            if (_quotation.Status == QuotationStatus.Sent)
            {
                _toastService.ShowInfo("Teklif zaten müşteriye gönderilmiş durumda.");
                return;
            }
            if (_quotation.Status != QuotationStatus.Draft)
            {
                _toastService.ShowWarning("Bu teklif gönderilemez; yalnızca taslak teklif gönderilir.");
                return;
            }

            var result = await _commandService.SendQuotationAsync(
                _quotation.Id, App.CurrentUser?.Username ?? "Sistem");
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            _toastService.ShowSuccess("Teklif müşteriye gönderildi. Müşteri cevabı (kabul/ret) artık kaydedilebilir.");
            RequestCloseWithSuccess?.Invoke();
        }

        [RelayCommand]
        private async Task Accept()
        {
            if (IsViewingHistoricRevision)
            {
                _toastService.ShowWarning("Geçmiş revizyon üzerinde işlem yapılamaz; önce güncel teklife dönün.");
                return;
            }
            if (_quotation.Status == QuotationStatus.Accepted)
            {
                _toastService.ShowInfo("Teklif zaten kabul edilmiş durumda.");
                return;
            }

            var result = await _commandService.AcceptQuotationAsync(_quotation.Id, "Kullanıcı");
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            _toastService.ShowSuccess("Teklif kabul edildi. 'Montaj Yapılacak' aşaması artık kullanılabilir.");
            RequestCloseWithSuccess?.Invoke();
        }

        [RelayCommand]
        private async Task Reject()
        {
            if (IsViewingHistoricRevision)
            {
                _toastService.ShowWarning("Geçmiş revizyon üzerinde işlem yapılamaz; önce güncel teklife dönün.");
                return;
            }
            string? reason = await _dialogService.ShowInputAsync("Red gerekçesi:", "Teklif Reddi", "Fiyat uygun bulunmadı.");
            if (reason is null) return;

            var result = await _commandService.RejectQuotationAsync(_quotation.Id, reason.Trim(), "Kullanıcı");
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            _toastService.ShowSuccess("Teklif reddedildi.");
            RequestCloseWithSuccess?.Invoke();
        }

        [RelayCommand]
        private async Task ViewRevision(QuotationRevisionSummaryDto? revision)
        {
            if (revision is null)
            {
                _toastService.ShowInfo("Görüntülemek için listeden bir revizyon seçin.");
                return;
            }

            // Güncel teklif zaten ekranda; onu kilitlemek yanlış olur (taslak/sent düzenlenebilir).
            if (revision.Id == _currentQuotationId)
            {
                _toastService.ShowInfo("Bu zaten güncel teklif; doğrudan düzenleyebilirsiniz.");
                return;
            }

            var result = await _readService.GetQuotationByIdAsync(revision.Id);
            if (result.IsFailure || result.Value is null)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            IsViewingHistoricRevision = revision.Id != _currentQuotationId;
            IsEditable = false;
            CanCreateRevision = false;
            IsViewMode = true;
            ViewingRevisionDisplay = IsViewingHistoricRevision
                ? $"Revizyon {revision.RevisionNumber} görüntüleniyor"
                : string.Empty;
            UpdateBanner();
            ApplyQuotation(result.Value);
            SaveCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private async Task BackToCurrent()
        {
            var result = await _readService.GetQuotationByIdAsync(_currentQuotationId);
            if (result.IsFailure || result.Value is null)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            var quote = result.Value;
            IsEditable = quote.Status is QuotationStatus.Draft or QuotationStatus.Sent;
            CanCreateRevision = quote.Status != QuotationStatus.Draft;
            IsViewingHistoricRevision = false;
            IsViewMode = !IsEditable;
            ViewingRevisionDisplay = string.Empty;
            UpdateBanner();
            ApplyQuotation(quote);
            SaveCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Kaydetmeden geçerli ekran durumunu (düzenlenmemiş değişiklikler dahil)
        /// geçici bir PDF'e basıp sistem görüntüleyicisinde açar.
        /// </summary>
        [RelayCommand]
        private async Task PreviewPdf()
        {
            try
            {
                var preview = new WorkOrderQuotationDto(
                    _quotation.Id,
                    _quotation.ServiceJobId,
                    _quotation.QuotationNumber,
                    _quotation.Status,
                    _quotation.IssuedDate,
                    _quotation.ValidUntil,
                    Description,
                    Warranty,
                    DeliveryTime,
                    PaymentTerms,
                    LaborCost,
                    ShippingCost,
                    DiscountAmount,
                    TaxRate,
                    TaxAmount,
                    TotalAmount,
                    _quotation.SentDate,
                    _quotation.AcceptedAt,
                    _quotation.RejectedAt,
                    _quotation.RejectionReason,
                    Items.Select(i => new QuotationItemDto(
                        i.SourceId ?? 0, i.ProductId, i.ProductName, i.Quantity,
                        i.UnitPrice, i.DiscountPercent, i.TaxPercent, i.LineNet, 0)).ToList(),
                    _quotation.RevisionNumber,
                    _quotation.ParentQuotationId);

                // Önizleme dosyası bilinçli olarak aynı geçici yola yazılır (her önizleme üzerine yazar);
                // açık görüntüleyici dosyayı okurken silinmemesi için temizlik yapılmaz.
                string filePath = Path.Combine(
                    Path.GetTempPath(), $"KamatekTeklifOnizleme_{_jobId}.pdf");
                _pdfService.GenerateWorkOrderQuotationPdf(preview, _jobDocument, filePath);
                Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
                _toastService.ShowInfo("Önizleme PDF'i açıldı (henüz kaydedilmemiş değişiklikler dahildir).");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Önizleme oluşturulamadı: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task PrintPdf()
        {
            var filePath = await _dialogService.ShowSaveFileDialogAsync(
                "Fiyat Teklifini Kaydet",
                "PDF Dosyası (*.pdf)|*.pdf",
                $"FiyatTeklifi_{_jobId:D6}.pdf");
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                _pdfService.GenerateWorkOrderQuotationPdf(_quotation, _jobDocument, filePath);
                Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
                _toastService.ShowSuccess("Fiyat teklifi PDF olarak oluşturuldu.");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"PDF oluşturulurken hata: {ex.Message}");
            }
        }

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke();
    }
}
