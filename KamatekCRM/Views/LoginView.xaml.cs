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

            Loaded += LoginView_Loaded;
            PreviewKeyDown += (_, _) => UpdateCapsLockState();
            PreviewKeyUp += (_, _) => UpdateCapsLockState();
            GotKeyboardFocus += (_, _) => UpdateCapsLockState();
        }

        private void LoginView_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateCapsLockState();

            if (DataContext is LoginViewModel vm && !string.IsNullOrWhiteSpace(vm.Username))
            {
                PasswordBox?.Focus();
            }
            else
            {
                UsernameTextBox?.Focus();
            }
        }

        private void UsernameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && PasswordBox != null && string.IsNullOrEmpty(PasswordBox.Password))
            {
                PasswordBox.Focus();
                e.Handled = true;
            }
        }

        private void UpdateCapsLockState()
        {
            if (DataContext is LoginViewModel vm)
            {
                vm.IsCapsLockOn = Keyboard.IsKeyToggled(Key.CapsLock);
            }
        }
    }
}
