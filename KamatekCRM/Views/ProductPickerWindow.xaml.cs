using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;

namespace KamatekCrm.Views
{
    /// <summary>
    /// Teklif düzenleyici için stok ürünü seçme penceresi. Arama metniyle ürün
    /// arar, çift tıklama veya "Ekle" ile seçimi döndürür.
    /// </summary>
    public partial class ProductPickerWindow : Window
    {
        private readonly Func<string, Task<IReadOnlyList<QuotationProductLookupDto>>> _search;
        private readonly DispatcherTimer _debounceTimer;
        private int _searchVersion;

        public ObservableCollection<QuotationProductLookupDto> Results { get; } = new();

        public QuotationProductLookupDto? SelectedProduct { get; private set; }

        public ProductPickerWindow(Func<string, Task<IReadOnlyList<QuotationProductLookupDto>>> search)
        {
            InitializeComponent();

            _search = search ?? throw new ArgumentNullException(nameof(search));
            ResultsGrid.ItemsSource = Results;

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _debounceTimer.Tick += async (_, _) =>
            {
                _debounceTimer.Stop();
                await SearchAsync(SearchBox.Text);
            };
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private async Task SearchAsync(string term)
        {
            if (term.Trim().Length < 2)
            {
                Results.Clear();
                StatusText.Text = "Aramak için en az 2 karakter girin.";
                AddButton.IsEnabled = false;
                return;
            }

            // Ardışık aramalarda geç gelen eski yanıtların sonuçları ezmesini engelle.
            int version = ++_searchVersion;
            try
            {
                var products = await _search(term.Trim());
                if (version != _searchVersion) return;

                Results.Clear();
                foreach (var product in products) Results.Add(product);

                StatusText.Text = products.Count == 0
                    ? "Sonuç bulunamadı."
                    : $"{products.Count} ürün bulundu.";
                AddButton.IsEnabled = ResultsGrid.SelectedItem is not null;
            }
            catch (Exception ex)
            {
                if (version != _searchVersion) return;
                StatusText.Text = $"Arama hatası: {ex.Message}";
            }
        }

        private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsGrid.SelectedItem is QuotationProductLookupDto product)
            {
                Confirm(product);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsGrid.SelectedItem is QuotationProductLookupDto product)
            {
                Confirm(product);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try { DialogResult = false; } catch { }
            Close();
        }

        private void Confirm(QuotationProductLookupDto product)
        {
            SelectedProduct = product;
            try { DialogResult = true; } catch { }
            Close();
        }
    }
}
