using System.Windows.Controls;
using KamatekCrm.Enums;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// Interaction logic for CustomersView.xaml
    /// </summary>
    public partial class CustomersView : UserControl
    {
        public CustomersView()
        {
            InitializeComponent();
        }

        private void OnIndividualChecked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is CustomersViewModel viewModel)
            {
                viewModel.NewCustomerType = CustomerType.Individual;
            }
        }

        private void OnCorporateChecked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is CustomersViewModel viewModel)
            {
                viewModel.NewCustomerType = CustomerType.Corporate;
            }
        }
    }
}
