using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KamatekCrm.Views
{
    public partial class CustomersView : UserControl
    {
        public CustomersView() => InitializeComponent();

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Foreground == Brushes.Gray)
            {
                textBox.Text = string.Empty;
                textBox.Foreground = Brushes.Black;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = textBox.Tag?.ToString() ?? "";
                textBox.Foreground = Brushes.Gray;
            }
        }



    }
    }

