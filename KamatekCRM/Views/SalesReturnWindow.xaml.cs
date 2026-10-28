using System.Windows;

namespace KamatekCrm.Views;

public partial class SalesReturnWindow : Window
{
    public SalesReturnWindow()
    {
        InitializeComponent();
        KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
