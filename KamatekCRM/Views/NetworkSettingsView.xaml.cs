using System.Windows.Controls;

namespace KamatekCrm.Views
{
    /// <summary>
    /// Ağ & Bağlantı Yönetimi UserControl — NetworkSettingsViewModel ile bağlanır.
    /// </summary>
    public partial class NetworkSettingsView : UserControl
    {
        public NetworkSettingsView()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Find the parent Window hosting this UserControl and close it
            var parentWindow = System.Windows.Window.GetWindow(this);
            parentWindow?.Close();
        }
    }
}
