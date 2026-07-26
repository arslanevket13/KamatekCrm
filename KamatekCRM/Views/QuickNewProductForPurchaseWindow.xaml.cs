using System.Windows;

namespace KamatekCrm.Views
{
    public partial class QuickNewProductForPurchaseWindow : Window
    {
        public QuickNewProductForPurchaseWindow()
        {
            InitializeComponent();
            var dbContextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext>>(App.ServiceProvider!);
            var vm = new KamatekCrm.ViewModels.QuickNewProductForPurchaseViewModel(dbContextFactory);
            vm.RequestClose += success =>
            {
                DialogResult = success;
                Close();
            };
            DataContext = vm;
            Loaded += (_, _) => ProductNameBox.Focus();
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
