using KamatekCrm.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Repositories;

namespace KamatekCrm.ViewModels
{
    public partial class SuppliersViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly Services.IToastService? _toastService;
        private readonly KamatekCrm.Shared.Services.IDialogService? _dialogService;
        private CancellationTokenSource? _searchCts;

        public SuppliersViewModel(
            IUnitOfWork unitOfWork,
            Services.IToastService? toastService = null,
            KamatekCrm.Shared.Services.IDialogService? dialogService = null)
        {
            _unitOfWork = unitOfWork;
            _toastService = toastService;
            _dialogService = dialogService;
            _ = Refresh();
        }

        #region Properties

        private ObservableCollection<Supplier> _suppliers = new ObservableCollection<Supplier>();
        public ObservableCollection<Supplier> Suppliers
        {
            get => _suppliers;
            set
            {
                if (SetProperty(ref _suppliers, value))
                {
                    OnPropertyChanged(nameof(FilteredSuppliers));
                }
            }
        }

        public ObservableCollection<Supplier> FilteredSuppliers => Suppliers;

        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set => SetProperty(ref _selectedSupplier, value);
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    DebouncedRefreshAsync().SafeFireAndForget("SuppliersViewModel.DebouncedRefreshAsync");
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        #endregion

        private bool CanSaveSupplier() => SelectedSupplier != null;
        private bool CanDeleteSupplier() => SelectedSupplier != null && SelectedSupplier.Id > 0;

        [RelayCommand]
        private void Clear()
        {
            SearchText = string.Empty;
        }

        [RelayCommand]
        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        #region Methods

        private async Task DebouncedRefreshAsync()
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            var cts = _searchCts = new CancellationTokenSource();
            try
            {
                await Task.Delay(200, cts.Token);
                await LoadSuppliersAsync(cts.Token);
            }
            catch (OperationCanceledException catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "SuppliersViewModel.cs"); }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            _searchCts?.Cancel();
            await LoadSuppliersAsync(CancellationToken.None);
        }

        private async Task LoadSuppliersAsync(CancellationToken cancellationToken)
        {
            IsBusy = true;
            try
            {
                var repo = _unitOfWork.Repository<Supplier>();
                var search = SearchText?.Trim();
                var list = string.IsNullOrWhiteSpace(search)
                    ? await repo.GetAllAsync(cancellationToken)
                    : await repo.FindAsync(
                        x => x.CompanyName.ToLower().Contains(search.ToLower())
                          || (x.ContactPerson != null && x.ContactPerson.ToLower().Contains(search.ToLower())),
                        cancellationToken);

                if (!cancellationToken.IsCancellationRequested)
                {
                    Suppliers = new ObservableCollection<Supplier>(list.OrderBy(x => x.CompanyName));
                }
            }
            catch (OperationCanceledException catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "SuppliersViewModel.cs"); }
            catch (Exception ex)
            {
                _toastService?.ShowError("Hata", $"Veri yüklenirken hata oluştu: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void AddSupplier()
        {
            SelectedSupplier = new Supplier
            {
                CompanyName = "Yeni Tedarikçi",
                IsActive = true
            };
        }

        [RelayCommand(CanExecute = nameof(CanSaveSupplier))]
        private async Task SaveSupplier()
        {
            if (SelectedSupplier == null) return;

            if (string.IsNullOrWhiteSpace(SelectedSupplier.CompanyName))
            {
                _toastService?.ShowWarning("Uyarı", "Firma ünvanı boş olamaz.");
                return;
            }

            IsBusy = true;
            try
            {
                var repo = _unitOfWork.Repository<Supplier>();
                if (SelectedSupplier.Id == 0)
                {
                    await repo.AddAsync(SelectedSupplier);
                }
                else
                {
                    repo.Update(SelectedSupplier);
                }

                await _unitOfWork.SaveChangesAsync();
                await Refresh();
                _toastService?.ShowSuccess("Bilgi", "Kayıt Başarılı.");
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Hata", $"Kaydetme hatası: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteSupplier))]
        private async Task DeleteSupplier()
        {
            if (SelectedSupplier == null) return;

            bool confirmed = _dialogService != null && await _dialogService.ShowConfirmationAsync(
                $"'{SelectedSupplier.CompanyName}' silinecek. Onaylıyor musunuz?",
                "Silme Onayı");
            if (confirmed)
            {
                IsBusy = true;
                try
                {
                    _unitOfWork.Repository<Supplier>().Remove(SelectedSupplier);
                    await _unitOfWork.SaveChangesAsync();
                    SelectedSupplier = null;
                    await Refresh();
                }
                catch (Exception ex)
                {
                    _toastService?.ShowError("Hata", $"Silme hatası: {ex.Message}");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        #endregion
    }
}
