using System.Windows;

namespace KamatekCrm.Views
{
    public partial class QuickCustomerAddWindow : Window
    {
        public QuickCustomerAddWindow(KamatekCrm.ViewModels.QuickCustomerAddViewModel viewModel)
        {
            InitializeComponent();
            viewModel.RequestClose += success =>
            {
                try { DialogResult = success; } catch { }
                Close();
            };
            DataContext = viewModel;
            Loaded += (_, _) => FullNameBox.Focus();
        }

        public QuickCustomerAddWindow()
        {
            InitializeComponent();
            if (App.ServiceProvider != null)
            {
                var dbContextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext>>(App.ServiceProvider);
                var vm = new KamatekCrm.ViewModels.QuickCustomerAddViewModel(dbContextFactory);
                vm.RequestClose += success =>
                {
                    try { DialogResult = success; } catch { }
                    Close();
                };
                DataContext = vm;
            }
            Loaded += (_, _) => FullNameBox.Focus();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}
