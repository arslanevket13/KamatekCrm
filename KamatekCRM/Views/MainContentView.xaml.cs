using System.Windows.Controls;
using System.Windows.Input;

namespace KamatekCrm.Views
{
    /// <summary>
    /// MainContentView.xaml code-behind
    /// </summary>
    public partial class MainContentView : UserControl
    {
        public MainContentView()
        {
            InitializeComponent();
            PreviewKeyDown += OnPreviewKeyDown;
        }

        /// <summary>
        /// Ctrl+F: global arama kutusuna odaklanır.
        /// (Ctrl+K hızlı ekleme, Ctrl+B kenar çubuğu, Ctrl+D tema kısayolları korunur.)
        /// </summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                GlobalSearchBox.Focus();
                Keyboard.Focus(GlobalSearchBox);
                e.Handled = true;
            }
        }
    }
}
