using System.Windows;
using System.Windows.Controls;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    public partial class FirstRunWizardView : UserControl
    {
        public FirstRunWizardView()
        {
            InitializeComponent();
        }

        private void AdminPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is FirstRunWizardViewModel vm && sender is PasswordBox pb)
            {
                vm.AdminPassword = pb.Password;
            }
        }

        private void AdminPasswordConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is FirstRunWizardViewModel vm && sender is PasswordBox pb)
            {
                vm.AdminPasswordConfirm = pb.Password;
            }
        }
    }
}
