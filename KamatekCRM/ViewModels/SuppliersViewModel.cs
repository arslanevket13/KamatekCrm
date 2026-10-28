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
        private CancellationTokenSource? _searchCts;

        public SuppliersViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
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
                    _ = DebouncedRefreshAsync();
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
            catch (OperationCanceledException)
            {
            }
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
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veri yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Firma ünvanı boş olamaz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("Kayıt Başarılı.", "Bilgi");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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

            var res = MessageBox.Show($"'{SelectedSupplier.CompanyName}' silinecek. Onaylıyor musunuz?", "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
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
                    MessageBox.Show($"Silme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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
