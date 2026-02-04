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
        public AddProductWindow() : this(null) { }

        /// <summary>
        /// Ürün ekleme veya düzenleme modu
        /// </summary>
        /// <param name="productToEdit">Düzenlenecek ürün. Null ise yeni ürün eklenir.</param>
        public AddProductWindow(Product? productToEdit)
        {
            InitializeComponent();
            
            var viewModel = new AddProductViewModel(productToEdit);
            viewModel.RequestClose += OnRequestClose;
            DataContext = viewModel;
            
            // Pencere başlığını güncelle
            Title = viewModel.WindowTitle;
        }

        private void OnRequestClose(bool result)
        {
            DialogResult = result;
            Close();
        }
    }
}
