using System.Windows.Controls;
using KamatekCrm.Enums;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// Interaction logic for CustomerDetailView.xaml
    /// </summary>
    public partial class CustomerDetailView : UserControl
    {
        public CustomerDetailView()
        {
            InitializeComponent();
        }

        private void OnIndividualChecked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is CustomerDetailViewModel viewModel)
            {
                viewModel.CustomerType = CustomerType.Individual;
            }
        }

        private void OnCorporateChecked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is CustomerDetailViewModel viewModel)
            {
                viewModel.CustomerType = CustomerType.Corporate;
            }
        }
    }
}
