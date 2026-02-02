using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Models;
using KamatekCrm.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    public class SuppliersViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;

        public ObservableCollection<Supplier> Suppliers { get; set; }

        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set => SetProperty(ref _selectedSupplier, value);
        }

        public ICommand LoadDataCommand { get; }

        public SuppliersViewModel(IUnitOfWork uow)
        {
            _uow = uow;
            Suppliers = new ObservableCollection<Supplier>();
            LoadDataCommand = new RelayCommand(async _ => await LoadDataAsync());
            
            // Auto load
            LoadDataCommand.Execute(null);
        }

        // Default constructor for design-time
        public SuppliersViewModel() : this(new UnitOfWork()) { }

        private async Task LoadDataAsync()
        {
            try 
            {
                var list = await _uow.Context.Suppliers.ToListAsync();
                Suppliers.Clear();
                foreach (var item in list)
                {
                    Suppliers.Add(item);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}");
            }
        }
    }
}
