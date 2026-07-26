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
            try { DialogResult = result; } catch { }
            Close();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            
            if (GetTemplateChild("PART_CloseButton") is System.Windows.Controls.Button closeButton)
            {
                closeButton.Click += (s, e) => this.Close();
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            try { DialogResult = false; } catch { }
            Close();
        }
    }
}
