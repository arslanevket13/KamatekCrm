using System.Windows;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// Hızlı cihaz ekleme penceresi
    /// </summary>
    public partial class QuickAssetAddWindow : Window
    {
        public CustomerAsset? CreatedAsset => (DataContext as QuickAssetAddViewModel)?.CreatedAsset;

        public QuickAssetAddWindow(int customerId)
        {
            InitializeComponent();
            var dbContextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Data.AppDbContext>>(App.ServiceProvider!);
            var viewModel = new QuickAssetAddViewModel(customerId, dbContextFactory);
            viewModel.RequestClose += (result) =>
            {
                DialogResult = result;
                Close();
            };
            
            DataContext = viewModel;
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
