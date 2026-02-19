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
        }
    }
}
