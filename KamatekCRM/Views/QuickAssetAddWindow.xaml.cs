using System.Windows;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;
using KamatekCrm.Infrastructure.Data;

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
            var dbContextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext>>(App.ServiceProvider!);
            var viewModel = new QuickAssetAddViewModel(customerId, dbContextFactory);
            viewModel.RequestClose += (result) =>
            {
                try { DialogResult = result; } catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "QuickAssetAddWindow.xaml.cs"); }
                Close();
            };
            
            DataContext = viewModel;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}
