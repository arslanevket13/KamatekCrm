using System.Windows;

namespace KamatekCrm.Views
{
    public partial class CustomerAddWindow : Window
    {
        public CustomerAddWindow()
        {
            InitializeComponent();
            var vm = new KamatekCrm.ViewModels.CustomerAddViewModel();
            vm.RequestClose += success =>
            {
                DialogResult = success;
                Close();
            };
            DataContext = vm;
        }

        public CustomerAddWindow(ViewModels.CustomerAddViewModel viewModel)
        {
            InitializeComponent();
            viewModel.RequestClose += success =>
            {
                DialogResult = success;
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
            this.DialogResult = false;
            this.Close();
        }
    }
}
