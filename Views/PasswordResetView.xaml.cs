using System.Windows;
using KamatekCrm.Models;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// PasswordResetView.xaml code-behind
    /// </summary>
    public partial class PasswordResetView : Window
    {
        public PasswordResetView(User user)
        {
            InitializeComponent();

            var viewModel = new PasswordResetViewModel(user);
            viewModel.SaveSuccessful += () =>
            {
                DialogResult = true;
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
