using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace KamatekCrm.Views
{
    /// <summary>
    /// StockCountView code-behind
    /// </summary>
    public partial class StockCountView : UserControl
    {
        public StockCountView()
        {
            InitializeComponent();
        }

        private void BarcodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox textBox)
            {
                // KeyBinding handles the command execution, select all text for rapid next scan
                textBox.SelectAll();
            }
        }
    }

    // Retain namespace aliases for XAML compatibility
    public class IsPositiveConverter : KamatekCrm.Converters.IsPositiveConverter { }
    public class IsNegativeConverter : KamatekCrm.Converters.IsNegativeConverter { }
    public class IsZeroConverter : KamatekCrm.Converters.IsZeroConverter { }
}
