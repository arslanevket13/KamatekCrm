using System.Windows;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// NewServiceJobWindow.xaml için code-behind
    /// </summary>
    public partial class NewServiceJobWindow : Window
    {
        public NewServiceJobWindow()
        {
            InitializeComponent();
            DataContext = new ServiceJobViewModel();
        }

        public NewServiceJobWindow(Customer? preselectedCustomer)
        {
            InitializeComponent();
            var vm = new ServiceJobViewModel();
            if (preselectedCustomer != null)
            {
                vm.SelectedCustomer = preselectedCustomer;
            }
            DataContext = vm;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
