using System.Windows;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// NewServiceJobWindow.xaml için code-behind
    /// </summary>
    public partial class NewServiceJobWindow : Window
    {
        public NewServiceJobWindow(ServiceJobViewModel vm)
        {
            InitializeComponent();
            
            vm.CancelRequested += () =>
            {
                DialogResult = false;
                Close();
            };

            vm.SaveCompleted += () =>
            {
                DialogResult = true;
                Close();
            };

            DataContext = vm;
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
