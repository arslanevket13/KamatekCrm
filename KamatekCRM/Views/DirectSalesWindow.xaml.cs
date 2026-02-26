using System.ComponentModel;
using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// DirectSalesWindow.xaml için etkileşim mantığı
    /// Perakende Satış (POS) Penceresi
    /// </summary>
    public partial class DirectSalesWindow : Window
    {
        public DirectSalesWindow(DirectSalesViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += DirectSalesWindow_Loaded;
            if (viewModel is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DirectSalesViewModel.FocusBarcodeRequested) && DataContext is DirectSalesViewModel vm)
            {
                if (vm.FocusBarcodeRequested)
                {
                    FocusBarcode();
                    vm.FocusBarcodeRequested = false;
                }
            }
        }

        private void DirectSalesWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BarcodeTextBox.Focus();
        }

        public void FocusBarcode()
        {
            BarcodeTextBox.Focus();
            BarcodeTextBox.SelectAll();
        }
    }
}
