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
        private bool _isPasswordVisible = false;

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

        private void PasswordToggle_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordTextBox.Visibility = Visibility.Visible;
                PasswordBox.Visibility = Visibility.Collapsed;
                EyePath.Data = (System.Windows.Media.Geometry)FindResource("EyeOffIcon");
            }
            else
            {
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                PasswordBox.Visibility = Visibility.Visible;
                EyePath.Data = (System.Windows.Media.Geometry)FindResource("EyeIcon");
            }

            if (DataContext is LoginViewModel vm)
            {
                vm.Password = _isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password;
            }
        }
    }
}
