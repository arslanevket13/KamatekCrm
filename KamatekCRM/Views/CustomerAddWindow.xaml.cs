using System.Windows;

namespace KamatekCrm.Views
{
    public partial class CustomerAddWindow : Window
    {
        public CustomerAddWindow()
        {
            InitializeComponent();
            var viewModel = App.ServiceProvider != null 
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ViewModels.CustomerAddViewModel>(App.ServiceProvider)
                : new ViewModels.CustomerAddViewModel(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Data.AppDbContext>>(App.ServiceProvider!));

            viewModel.RequestClose += success =>
            {
                try { DialogResult = success; } catch { }
                Close();
            };
            DataContext = viewModel;
        }

        public CustomerAddWindow(ViewModels.CustomerAddViewModel viewModel)
        {
            InitializeComponent();
            viewModel.RequestClose += success =>
            {
                try { DialogResult = success; } catch { }
                Close();
            };
            DataContext = viewModel;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            
            // PremiumWindowStyle içindeki kapatma butonunu bul ve Click eventini bağla
            if (GetTemplateChild("PART_CloseButton") is System.Windows.Controls.Button closeButton)
            {
                closeButton.Click += (s, e) => this.Close();
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            try { this.DialogResult = false; } catch { }
            this.Close();
        }
    }
}
