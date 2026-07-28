using System.Windows;
using System.Windows.Controls;

namespace KamatekCrm.Views
{
    public partial class QuotationWindow : Window
    {
        public QuotationWindow(KamatekCrm.ViewModels.QuotationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public QuotationWindow()
        {
            InitializeComponent();
            if (App.ServiceProvider != null)
            {
                var dbContextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext>>(App.ServiceProvider);
                DataContext = new KamatekCrm.ViewModels.QuotationViewModel(dbContextFactory);
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
