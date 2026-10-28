using System.Windows;
namespace KamatekCrm.Views;
public partial class PurchaseReturnWindow : Window
{
    public PurchaseReturnWindow()
    {
        InitializeComponent();
        KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
