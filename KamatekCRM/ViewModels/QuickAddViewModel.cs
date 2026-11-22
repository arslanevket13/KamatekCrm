using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    public sealed class QuickAddActionItem
    {
        public string Id { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Icon { get; init; } = string.Empty;
        public string? ShortcutHint { get; init; }
        public string Keywords { get; init; } = string.Empty;
        public bool HasShortcut => !string.IsNullOrWhiteSpace(ShortcutHint);
    }

    /// <summary>
    /// Hızlı Aksiyon & Komut Paleti (Ctrl+K) ViewModel.
    /// Öğeleri aktif kullanıcının RBAC izinlerine göre filtreler, arama ve klavye navigasyonunu yönetir.
    /// </summary>
    public partial class QuickAddViewModel : ViewModelBase
    {
        private static readonly CultureInfo TrCulture = new("tr-TR");
        private readonly List<QuickAddActionItem> _authorizedItems = new();

        public ObservableCollection<QuickAddActionItem> FilteredItems { get; } = new();

        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    ApplyFilter();
                }
            }
        }

        private QuickAddActionItem? _selectedItem;
        public QuickAddActionItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    ExecuteSelectedCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HasNoResults => FilteredItems.Count == 0;

        public event Action<string>? ActionChosen;
        public event Action? CloseRequested;

        public QuickAddViewModel(IAuthService authService)
        {
            BuildAuthorizedItems(authService);
            ApplyFilter();
        }

        private void BuildAuthorizedItems(IAuthService authService)
        {
            _authorizedItems.Clear();

            // Hızlı Aksiyonlar
            _authorizedItems.Add(new QuickAddActionItem
            {
                Id = "OpenFaultTicket",
                Category = "HIZLI AKSİYON",
                Title = "Arıza Kabul",
                Description = "Yeni cihaz arıza kaydı oluştur",
                Icon = "⚡",
                Keywords = "ariza cihaz kabul tamir servis yeni"
            });

            _authorizedItems.Add(new QuickAddActionItem
            {
                Id = "OpenDirectSales",
                Category = "HIZLI AKSİYON",
                Title = "Hızlı Satış (POS)",
                Description = "Perakende tezgah satışı yap",
                Icon = "🛒",
                Keywords = "pos kasa satis perakende barkod"
            });

            _authorizedItems.Add(new QuickAddActionItem
            {
                Id = "NewCustomer",
                Category = "HIZLI AKSİYON",
                Title = "Yeni Müşteri",
                Description = "Müşteri ekranına git / kayıt ekle",
                Icon = "👤",
                Keywords = "musteri yeni ekle cari kayit"
            });

            _authorizedItems.Add(new QuickAddActionItem
            {
                Id = "OpenProjectQuote",
                Category = "HIZLI AKSİYON",
                Title = "Proje & Teklif",
                Description = "Proje veya teklif oluştur",
                Icon = "🏗️",
                Keywords = "proje teklif fiyat kesif"
            });

            // Sayfalar (RBAC kontrollü)
            _authorizedItems.Add(new QuickAddActionItem
            {
                Id = "NavigateDashboard",
                Category = "SAYFA",
                Title = "Ana Sayfa (Dashboard)",
                Description = "Genel durum ve özet göstergeler",
                Icon = "🏠",
                Keywords = "dashboard anasayfa ozet kpi"
            });

            _authorizedItems.Add(new QuickAddActionItem
            {
                Id = "NavigateCustomers",
                Category = "SAYFA",
                Title = "Müşteriler",
                Description = "Müşteri listesi ve cari hesaplar",
                Icon = "👥",
                Keywords = "musteriler cari liste"
            });

            _authorizedItems.Add(new QuickAddActionItem
            {
                Id = "NavigateProducts",
                Category = "SAYFA",
                Title = "Ürünler ve Stok",
                Description = "Stok kartları ve depo envanteri",
                Icon = "📦",
                Keywords = "stok urun envanter depo"
            });

            _authorizedItems.Add(new QuickAddActionItem
            {
                Id = "NavigateRepairList",
                Category = "SAYFA",
                Title = "Tamir Listesi",
                Description = "Atölye ve cihaz tamir kayıtları",
                Icon = "🔧",
                Keywords = "tamir onarim cihaz atolye"
            });

            if (authService.CanViewFinance)
            {
                _authorizedItems.Add(new QuickAddActionItem
                {
                    Id = "NavigateFinance",
                    Category = "SAYFA",
                    Title = "Finans & Kasa",
                    Description = "Günlük kasa hareketleri ve tahsilatlar",
                    Icon = "💰",
                    Keywords = "finans kasa gelir gider nakit"
                });

                _authorizedItems.Add(new QuickAddActionItem
                {
                    Id = "NavigateFinancialHealth",
                    Category = "SAYFA",
                    Title = "Finansal Rapor",
                    Description = "Kârlılık ve finansal sağlık analizi",
                    Icon = "📈",
                    Keywords = "finansal rapor karlilik bilanco"
                });
            }

            if (authService.CanViewAnalytics)
            {
                _authorizedItems.Add(new QuickAddActionItem
                {
                    Id = "NavigateAnalytics",
                    Category = "SAYFA",
                    Title = "Analitik",
                    Description = "Operasyon ve performans grafikleri",
                    Icon = "📊",
                    Keywords = "analitik grafik istatistik rapor"
                });
            }

            if (authService.CanAccessSettings)
            {
                _authorizedItems.Add(new QuickAddActionItem
                {
                    Id = "NavigateSettings",
                    Category = "YÖNETİM",
                    Title = "Sistem Ayarları",
                    Description = "Firma, tema, yazıcı ve yedekleme ayarları",
                    Icon = "⚙️",
                    Keywords = "ayarlar sistem tema yedekleme firma"
                });
            }

            if (authService.IsAdmin)
            {
                _authorizedItems.Add(new QuickAddActionItem
                {
                    Id = "NavigateUsers",
                    Category = "YÖNETİM",
                    Title = "Kullanıcı Yönetimi",
                    Description = "Personel hesapları ve yetkiler",
                    Icon = "🛡️",
                    Keywords = "kullanici personel yetki rol admin"
                });

                _authorizedItems.Add(new QuickAddActionItem
                {
                    Id = "NavigateSystemLogs",
                    Category = "YÖNETİM",
                    Title = "Sistem Kayıtları",
                    Description = "Denetim izi ve işlem logları",
                    Icon = "📜",
                    Keywords = "log denetim kayit guvenlik"
                });
            }
        }

        private void ApplyFilter()
        {
            var previousId = SelectedItem?.Id;
            FilteredItems.Clear();

            string query = SearchQuery?.Trim() ?? string.Empty;
            IEnumerable<QuickAddActionItem> matches = _authorizedItems;

            if (!string.IsNullOrEmpty(query))
            {
                var qLower = query.ToLower(TrCulture);
                matches = _authorizedItems.Where(item =>
                    item.Title.ToLower(TrCulture).Contains(qLower) ||
                    item.Description.ToLower(TrCulture).Contains(qLower) ||
                    item.Category.ToLower(TrCulture).Contains(qLower) ||
                    item.Keywords.ToLower(TrCulture).Contains(qLower));
            }

            foreach (var item in matches)
            {
                FilteredItems.Add(item);
            }

            OnPropertyChanged(nameof(HasNoResults));

            SelectedItem = (!string.IsNullOrEmpty(previousId)
                ? FilteredItems.FirstOrDefault(i => i.Id == previousId)
                : null) ?? FilteredItems.FirstOrDefault();
        }

        [RelayCommand]
        private void MoveSelectionUp()
        {
            if (FilteredItems.Count == 0) return;

            int index = SelectedItem != null ? FilteredItems.IndexOf(SelectedItem) : -1;
            int nextIndex = index <= 0 ? FilteredItems.Count - 1 : index - 1;
            SelectedItem = FilteredItems[nextIndex];
        }

        [RelayCommand]
        private void MoveSelectionDown()
        {
            if (FilteredItems.Count == 0) return;

            int index = SelectedItem != null ? FilteredItems.IndexOf(SelectedItem) : -1;
            int nextIndex = index < 0 || index >= FilteredItems.Count - 1 ? 0 : index + 1;
            SelectedItem = FilteredItems[nextIndex];
        }

        private bool CanExecuteSelected() => SelectedItem != null;

        [RelayCommand(CanExecute = nameof(CanExecuteSelected))]
        private void ExecuteSelected()
        {
            if (SelectedItem == null) return;
            ActionChosen?.Invoke(SelectedItem.Id);
            CloseRequested?.Invoke();
        }

        [RelayCommand]
        private void SelectAction(QuickAddActionItem? item)
        {
            if (item == null) return;
            SelectedItem = item;
            ExecuteSelected();
        }

        [RelayCommand]
        private void Close()
        {
            CloseRequested?.Invoke();
        }
    }
}
