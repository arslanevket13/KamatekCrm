using System.Windows;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;
using KamatekCrm.Services;

namespace KamatekCrm.Views
{
    /// <summary>
    /// EditUserView.xaml code-behind
    /// </summary>
    public partial class EditUserView : Window
    {
        public EditUserView()
        {
            InitializeComponent();
        }

        public EditUserView(User user)
        {
            InitializeComponent();
            var dbContextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext>>(App.ServiceProvider!);
            var toastService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IToastService>(App.ServiceProvider!);
            var loadingService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILoadingService>(App.ServiceProvider!);
            DataContext = new EditUserViewModel(user, dbContextFactory, toastService, loadingService);
        }

        public EditUserView(User user, Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext> dbContextFactory, IToastService toastService, ILoadingService loadingService)
        {
            InitializeComponent();
            DataContext = new EditUserViewModel(user, dbContextFactory, toastService, loadingService);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}
