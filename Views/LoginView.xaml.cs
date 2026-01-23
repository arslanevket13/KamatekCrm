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

        /// <summary>
        /// Şifre değiştiğinde ViewModel'e aktar
        /// </summary>
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel viewModel)
            {
                viewModel.Password = PasswordBox.Password;
            }
        }

        /// <summary>
        /// Login butonuna tıklandığında
        /// </summary>
        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel viewModel)
            {
                viewModel.Password = PasswordBox.Password;
                viewModel.ExecuteLogin();
            }
        }
    }
}
