using System.Windows;

namespace KamatekCrm
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent(); // Bu çağrı XAML'i yüklemek için zorunludur.
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        // private void InitializeComponent() { ... } <-- BU BLOK TAMAMEN SİLİNMELİDİR.
        // Bu metodu manuel olarak tanımlamayın.
    }
}