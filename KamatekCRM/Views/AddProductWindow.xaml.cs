using System.Windows;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// AddProductWindow.xaml için etkileşim mantığı
    /// Add ve Edit modlarını destekler
    /// </summary>
    public partial class AddProductWindow : Window
    {
        /// <summary>
        /// Yeni ürün ekleme modu
        /// </summary>
        public AddProductWindow()
        {
            InitializeComponent();

            DataContextChanged += (s, e) =>
            {
                if (DataContext is AddProductViewModel vm)
                {
                    vm.RequestClose -= OnRequestClose;
                    vm.RequestClose += OnRequestClose;
                    Title = vm.WindowTitle;
                }
            };
        }

        private void OnRequestClose(bool result)
        {
            try { System.Windows.Input.Keyboard.ClearFocus(); } catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "AddProductWindow.xaml.cs"); }
            try { DialogResult = result; } catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "AddProductWindow.xaml.cs"); }
            Close();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }

        private void BarcodeTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Donanımsal barkod okuyucu CR/Enter gönderdiğinde pencerenin yanlışlıkla kaydedip kapanmasını engelle
            if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Return)
            {
                e.Handled = true;
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            try { System.Windows.Input.Keyboard.ClearFocus(); } catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "AddProductWindow.xaml.cs"); }
            try { DialogResult = false; } catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "AddProductWindow.xaml.cs"); }
            Close();
        }
    }
}
