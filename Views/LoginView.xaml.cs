using System.Windows;
using System.Windows.Controls;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// LoginView.xaml code-behind (UserControl)
    /// </summary>
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();

            // Focus ayarı
            Loaded += (s, e) => UsernameTextBox?.Focus();
        }


    }
}
