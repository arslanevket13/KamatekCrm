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
        public PasswordResetView(User user, IAuthService authService, Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Data.AppDbContext> dbContextFactory)
        {
            InitializeComponent();

            var viewModel = new PasswordResetViewModel(user, authService, dbContextFactory);
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

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            
            if (GetTemplateChild("PART_CloseButton") is System.Windows.Controls.Button closeButton)
            {
                closeButton.Click += (s, e) => this.Close();
            }
        }
    }
}
