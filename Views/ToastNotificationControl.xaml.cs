using System.Windows;
using System.Windows.Controls;
using KamatekCrm.Services;

namespace KamatekCrm.Views
{
    /// <summary>
    /// ToastNotificationControl code-behind
    /// </summary>
    public partial class ToastNotificationControl : UserControl
    {
        public ToastNotificationControl()
        {
            InitializeComponent();
        }

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ToastMessage toast)
            {
                ToastNotificationManager.Instance.DismissToast(toast);
            }
        }
    }
}
