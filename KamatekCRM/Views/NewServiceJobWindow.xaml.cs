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
                try { DialogResult = false; } catch { }
                Close();
            };

            vm.SaveCompleted += () =>
            {
                try { DialogResult = true; } catch { }
                Close();
            };

            DataContext = vm;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}
