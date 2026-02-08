using System.Windows;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// Hızlı cihaz ekleme penceresi
    /// </summary>
    public partial class QuickAssetAddWindow : Window
    {
        public CustomerAsset? CreatedAsset => (DataContext as QuickAssetAddViewModel)?.CreatedAsset;

        public QuickAssetAddWindow(int customerId)
        {
            InitializeComponent();
            
            var viewModel = new QuickAssetAddViewModel(customerId);
            viewModel.RequestClose += (result) =>
            {
                DialogResult = result;
                Close();
            };
            
            DataContext = viewModel;
        }
    }
}
