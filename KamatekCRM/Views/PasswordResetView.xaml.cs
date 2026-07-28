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
        public PasswordResetView(User user)
        {
            InitializeComponent();
            var authService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IAuthService>(App.ServiceProvider!);
            var dbContextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext>>(App.ServiceProvider!);

            var viewModel = new PasswordResetViewModel(user, authService, dbContextFactory);
            viewModel.SaveSuccessful += () =>
            {
                try { DialogResult = true; } catch { }
                Close();
            };
            
            viewModel.CancelRequested += () =>
            {
                try { DialogResult = false; } catch { }
                Close();
            };

            DataContext = viewModel;
        }

        public PasswordResetView(User user, IAuthService authService, Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext> dbContextFactory)
        {
            InitializeComponent();

            var viewModel = new PasswordResetViewModel(user, authService, dbContextFactory);
            viewModel.SaveSuccessful += () =>
            {
                try { DialogResult = true; } catch { }
                Close();
            };
            
            viewModel.CancelRequested += () =>
            {
                try { DialogResult = false; } catch { }
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
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}
