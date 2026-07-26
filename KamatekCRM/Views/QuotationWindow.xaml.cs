using System.Windows;
using System.Windows.Controls;

namespace KamatekCrm.Views
{
    public partial class QuotationWindow : Window
    {
        public QuotationWindow()
        {
            InitializeComponent();
            var dbContextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext>>(App.ServiceProvider);
            DataContext = new KamatekCrm.ViewModels.QuotationViewModel(dbContextFactory);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            
            // PremiumWindowStyle içindeki kapatma butonunu bul ve Click eventini bağla
            if (GetTemplateChild("PART_CloseButton") is Button closeButton)
            {
                closeButton.Click += (s, e) => this.Close();
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
