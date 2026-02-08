using System.Windows;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;
using KamatekCrm.Services;

namespace KamatekCrm.Views
{
    /// <summary>
    /// PasswordResetView.xaml code-behind
    /// </summary>
    public partial class PasswordResetView : Window
    {
        public PasswordResetView(User user, IAuthService authService)
        {
            InitializeComponent();

            var viewModel = new PasswordResetViewModel(user, authService);
            viewModel.SaveSuccessful += () =>
            {
                DialogResult = true;
                Close();
            };
            
            viewModel.CancelRequested += () =>
            {
                DialogResult = false;
                Close();
            };

            DataContext = viewModel;
        }

        private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is PasswordResetViewModel vm)
            {
                vm.NewPassword = NewPasswordBox.Password;
            }
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is PasswordResetViewModel vm)
            {
                vm.ConfirmPassword = ConfirmPasswordBox.Password;
            }
        }
    }
}
