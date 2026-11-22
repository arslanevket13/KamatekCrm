using System;
using System.Diagnostics;
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
            : this(
                user,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IAuthService>(App.ServiceProvider!),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext>>(App.ServiceProvider!),
                isSelfServiceForcedChange: false)
        {
        }

        public PasswordResetView(
            User user,
            IAuthService authService,
            Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext> dbContextFactory,
            bool isSelfServiceForcedChange = false)
        {
            InitializeComponent();

            var viewModel = new PasswordResetViewModel(user, authService, dbContextFactory, isSelfServiceForcedChange);
            viewModel.SaveSuccessful += () => CloseWithResult(true);
            viewModel.CancelRequested += () => CloseWithResult(false);

            DataContext = viewModel;
            Loaded += (_, _) => NewPasswordBox?.Focus();
        }

        private void CloseWithResult(bool result)
        {
            try
            {
                DialogResult = result;
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"PasswordResetView DialogResult atanamadı: {ex.Message}");
            }

            Close();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}
