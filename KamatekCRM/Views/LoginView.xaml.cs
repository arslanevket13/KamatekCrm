using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

            Loaded += (s, e) => UsernameTextBox?.Focus();
        }

        private void ForgotPassword_Click(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show(
                "Şifre sıfırlama özelliği henüz aktif değil.\nLütfen sistem yöneticinizle iletişime geçin.",
                "Şifremi Unuttum",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
