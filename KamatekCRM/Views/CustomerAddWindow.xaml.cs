using System.Windows;

namespace KamatekCrm.Views
{
    public partial class CustomerAddWindow : Window
    {
        public CustomerAddWindow()
        {
            InitializeComponent();
            if (App.ServiceProvider == null)
            {
                return;
            }

            var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<ViewModels.CustomerAddViewModel>(App.ServiceProvider);

            viewModel.RequestClose += success =>
            {
                try { DialogResult = success; } catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "CustomerAddWindow.xaml.cs"); }
                Close();
            };
            DataContext = viewModel;
        }

        public CustomerAddWindow(ViewModels.CustomerAddViewModel viewModel)
        {
            InitializeComponent();
            viewModel.RequestClose += success =>
            {
                try { DialogResult = success; } catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "CustomerAddWindow.xaml.cs"); }
                Close();
            };
            DataContext = viewModel;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            try { this.DialogResult = false; } catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "CustomerAddWindow.xaml.cs"); }
            this.Close();
        }
    }
}
