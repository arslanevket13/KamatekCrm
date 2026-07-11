using System.Windows;

namespace KamatekCrm.Views
{
    public partial class CustomerDetailWindow : Window
    {
        public CustomerDetailWindow()
        {
            InitializeComponent();
        }

        public void Initialize(int customerId)
        {
            if (DetailView.DataContext is ViewModels.CustomerDetailViewModel vm)
            {
                vm.Initialize(customerId);
            }
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
