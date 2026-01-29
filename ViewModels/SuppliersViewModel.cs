using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using KamatekCrm.Repositories;
using KamatekCrm.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Tedarikçi Yönetimi ViewModel - IUnitOfWork ve Async/Await desteği ile
    /// </summary>
    public class SuppliersViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly AttachmentService _attachmentService;

        #region Collections

        public ObservableCollection<Supplier> Suppliers { get; } = new();
        public ObservableCollection<Supplier> FilteredSuppliers { get; } = new();
        public ObservableCollection<PurchaseOrder> SupplierPurchaseHistory { get; } = new();
        public ObservableCollection<Attachment> SupplierAttachments { get; } = new();

        /// <summary>
        /// Tedarikçi Tipi seçenekleri (ComboBox için)
        /// </summary>
        public Array SupplierTypes => Enum.GetValues(typeof(SupplierType));

        #endregion

        #region Filter Properties

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilter();
            }
        }

        private bool _showDebtorsOnly;
        /// <summary>
        /// Sadece borçlu tedarikçileri göster (Balance > 0)
        /// </summary>
        public bool ShowDebtorsOnly
        {
            get => _showDebtorsOnly;
            set
            {
                if (SetProperty(ref _showDebtorsOnly, value))
                    ApplyFilter();
            }
        }

        private bool _showInactiveSuppliers;
        /// <summary>
        /// Pasif tedarikçileri de göster
        /// </summary>
        public bool ShowInactiveSuppliers
        {
            get => _showInactiveSuppliers;
            set
            {
                if (SetProperty(ref _showInactiveSuppliers, value))
                    _ = LoadDataAsync();
            }
        }

        private SupplierType? _selectedSupplierTypeFilter;
        /// <summary>
        /// Tip bazlı filtreleme
        /// </summary>
        public SupplierType? SelectedSupplierTypeFilter
        {
            get => _selectedSupplierTypeFilter;
            set
            {
                if (SetProperty(ref _selectedSupplierTypeFilter, value))
                    ApplyFilter();
            }
        }

        #endregion

        #region State Properties

        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value))
                {
                    if (value != null)
                    {
                        IsEditing = true;
                        _ = LoadPurchaseHistoryAsync();
                        LoadAttachments();
                    }
                    else
                    {
                        IsEditing = false;
                        SupplierPurchaseHistory.Clear();
                        SupplierAttachments.Clear();
                    }
                    OnPropertyChanged(nameof(SupplierPurchaseHistory));
                    OnPropertyChanged(nameof(SupplierAttachments));
                }
            }
        }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private Attachment? _selectedAttachment;
        public Attachment? SelectedAttachment
        {
            get => _selectedAttachment;
            set => SetProperty(ref _selectedAttachment, value);
        }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CreateNewCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand ClearTypeFilterCommand { get; }
        public ICommand ViewOrderDetailCommand { get; }
        
        // Attachment Commands
        public ICommand AddAttachmentCommand { get; }
        public ICommand RemoveAttachmentCommand { get; }
        public ICommand OpenAttachmentCommand { get; }

        #endregion

        #region Constructor

        public SuppliersViewModel() : this(new UnitOfWork()) { }

        public SuppliersViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _attachmentService = new AttachmentService(_unitOfWork.Context);

            // Command tanımlamaları
            SaveCommand = new RelayCommand(async _ => await SaveChangesAsync(), _ => CanSave());
            CreateNewCommand = new RelayCommand(_ => CreateNew());
            DeleteCommand = new RelayCommand(async p => await DeleteSupplierAsync(p), CanDelete);
            RefreshCommand = new RelayCommand(async _ => await LoadDataAsync());
            ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);
            ClearTypeFilterCommand = new RelayCommand(_ => SelectedSupplierTypeFilter = null);
            ViewOrderDetailCommand = new RelayCommand(ViewOrderDetail, _ => SelectedSupplier != null);
            
            // Attachment Commands
            AddAttachmentCommand = new RelayCommand(_ => AddAttachment(), _ => SelectedSupplier != null && SelectedSupplier.Id > 0);
            RemoveAttachmentCommand = new RelayCommand(RemoveAttachment, _ => SelectedAttachment != null);
            OpenAttachmentCommand = new RelayCommand(OpenAttachment, _ => SelectedAttachment != null);

            // Veri yükleme
            _ = LoadDataAsync();
        }

        #endregion

        #region Data Operations

        private void ApplyFilter()
        {
            FilteredSuppliers.Clear();

            IEnumerable<Supplier> filtered = Suppliers;

            // Metin araması
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLowerInvariant();
                filtered = filtered.Where(s =>
                    (s.CompanyName?.ToLowerInvariant().Contains(search) ?? false) ||
                    (s.ContactPerson?.ToLowerInvariant().Contains(search) ?? false) ||
                    (s.PhoneNumber?.Contains(SearchText) ?? false) ||
                    (s.Email?.ToLowerInvariant().Contains(search) ?? false));
            }

            // Borçlu filtresi
            if (ShowDebtorsOnly)
            {
                filtered = filtered.Where(s => s.Balance > 0);
            }

            // Tip filtresi
            if (SelectedSupplierTypeFilter.HasValue)
            {
                filtered = filtered.Where(s => s.SupplierType == SelectedSupplierTypeFilter.Value);
            }

            foreach (var supplier in filtered)
            {
                FilteredSuppliers.Add(supplier);
            }
        }

        private async Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                Suppliers.Clear();
                FilteredSuppliers.Clear();

                var query = _unitOfWork.Context.Suppliers.AsQueryable();

                // Pasif filtreleme
                if (!ShowInactiveSuppliers)
                {
                    query = query.Where(s => s.IsActive);
                }

                var list = await query
                    .OrderBy(s => s.CompanyName)
                    .ToListAsync();

                foreach (var item in list)
                {
                    Suppliers.Add(item);
                }

                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veri yükleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadPurchaseHistoryAsync()
        {
            SupplierPurchaseHistory.Clear();
            if (SelectedSupplier == null) return;

            try
            {
                var orders = await _unitOfWork.Context.PurchaseOrders
                    .Where(p => p.SupplierName == SelectedSupplier.CompanyName)
                    .OrderByDescending(p => p.OrderDate)
                    .Take(50)
                    .ToListAsync();

                foreach (var po in orders)
                    SupplierPurchaseHistory.Add(po);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sipariş geçmişi yüklenemedi: {ex.Message}");
            }
        }

        private void CreateNew()
        {
            var newSupplier = new Supplier
            {
                CompanyName = "Yeni Tedarikçi",
                SupplierType = SupplierType.Wholesaler,
                PaymentTermDays = 0,
                IsActive = true,
                Balance = 0
            };

            _unitOfWork.Context.Suppliers.Add(newSupplier);
            Suppliers.Add(newSupplier);
            FilteredSuppliers.Add(newSupplier);
            SelectedSupplier = newSupplier;
            IsEditing = true;
        }

        private bool CanSave()
        {
            return SelectedSupplier != null && !string.IsNullOrWhiteSpace(SelectedSupplier.CompanyName);
        }

        private async Task SaveChangesAsync()
        {
            if (SelectedSupplier == null) return;

            IsBusy = true;
            try
            {
                await _unitOfWork.SaveChangesAsync();
                ApplyFilter();

                MessageBox.Show("Tedarikçi bilgileri başarıyla kaydedildi.", "Başarılı",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (DbUpdateException dbEx)
            {
                MessageBox.Show($"Veritabanı kayıt hatası: {dbEx.InnerException?.Message ?? dbEx.Message}",
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kayıt hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanDelete(object? parameter)
        {
            return SelectedSupplier != null && AuthService.CanDeleteRecords;
        }

        private async Task DeleteSupplierAsync(object? parameter)
        {
            if (SelectedSupplier == null) return;

            var result = MessageBox.Show(
                $"'{SelectedSupplier.CompanyName}' tedarikçisini silmek istediğinize emin misiniz?\n\nBu işlem geri alınamaz.",
                "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                // Soft delete
                SelectedSupplier.IsActive = false;
                await _unitOfWork.SaveChangesAsync();

                Suppliers.Remove(SelectedSupplier);
                FilteredSuppliers.Remove(SelectedSupplier);
                SelectedSupplier = null;

                MessageBox.Show("Tedarikçi başarıyla silindi (pasife alındı).", "Bilgi",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Silme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ViewOrderDetail(object? parameter)
        {
            if (parameter is PurchaseOrder order)
            {
                // PurchaseOrderView'a yönlendirme yapılabilir
                MessageBox.Show($"Sipariş Detayı: {order.PONumber}\nTarih: {order.OrderDate:dd.MM.yyyy}\nTutar: ₺{order.TotalAmount:N2}",
                    "Sipariş Detayı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Attachment Operations

        private void LoadAttachments()
        {
            SupplierAttachments.Clear();
            if (SelectedSupplier == null || SelectedSupplier.Id <= 0) return;

            try
            {
                var attachments = _attachmentService.GetAttachments(AttachmentEntityType.Supplier, SelectedSupplier.Id);
                foreach (var att in attachments)
                    SupplierAttachments.Add(att);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ekler yüklenemedi: {ex.Message}");
            }
        }

        private void AddAttachment()
        {
            if (SelectedSupplier == null || SelectedSupplier.Id <= 0)
            {
                MessageBox.Show("Önce tedarikçiyi kaydedin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Belge Ekle",
                Filter = "Tüm Dosyalar (*.*)|*.*|PDF Dosyaları (*.pdf)|*.pdf|Resim Dosyaları (*.jpg;*.png)|*.jpg;*.png",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    foreach (var filePath in dialog.FileNames)
                    {
                        var attachment = _attachmentService.UploadFile(
                            AttachmentEntityType.Supplier,
                            SelectedSupplier.Id,
                            filePath,
                            $"Tedarikçi belgesi: {Path.GetFileName(filePath)}");
                        
                        SupplierAttachments.Add(attachment);
                    }
                    MessageBox.Show($"{dialog.FileNames.Length} dosya başarıyla eklendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Dosya ekleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RemoveAttachment(object? parameter)
        {
            if (SelectedAttachment == null) return;

            var result = MessageBox.Show(
                $"'{SelectedAttachment.FileName}' dosyasını silmek istediğinize emin misiniz?",
                "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (_attachmentService.DeleteAttachment(SelectedAttachment.Id))
                {
                    SupplierAttachments.Remove(SelectedAttachment);
                    SelectedAttachment = null;
                    MessageBox.Show("Dosya silindi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Silme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenAttachment(object? parameter)
        {
            var attachment = parameter as Attachment ?? SelectedAttachment;
            if (attachment == null) return;

            try
            {
                _attachmentService.OpenFile(attachment);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dosya açılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}

