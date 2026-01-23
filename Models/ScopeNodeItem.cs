using System;
using System.Text.Json.Serialization;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Kapsam kalemi - Bir node'a atanmış ürün/hizmet
    /// Finansal derinlik: Alış fiyatı, Satış fiyatı, İşçilik, Kar Marjı
    /// </summary>
    public class ScopeNodeItem : ViewModelBase
    {
        private int _quantity = 1;
        private decimal _unitCost;
        private decimal _unitPrice;
        private decimal _laborCost;
        private bool _isOptional;
        private string _note = string.Empty;

        #region Properties (JSON Serialized)

        /// <summary>
        /// Benzersiz tanımlayıcı
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

        /// <summary>
        /// Ürün ID (Products tablosundan)
        /// </summary>
        public int ProductId { get; set; }

        /// <summary>
        /// Ürün adı (görüntüleme için)
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Ürün kodu / SKU
        /// </summary>
        public string? ProductSKU { get; set; }

        /// <summary>
        /// Birim (Adet, mt, tk vb.)
        /// </summary>
        public string Unit { get; set; } = "Adet";

        /// <summary>
        /// Adet
        /// </summary>
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, Math.Max(1, value)))
                {
                    NotifyFinancialsChanged();
                }
            }
        }

        /// <summary>
        /// Alış Fiyatı (Birim maliyet)
        /// </summary>
        public decimal UnitCost
        {
            get => _unitCost;
            set
            {
                if (SetProperty(ref _unitCost, value))
                {
                    NotifyFinancialsChanged();
                }
            }
        }

        /// <summary>
        /// Satış Fiyatı (Birim fiyat)
        /// </summary>
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetProperty(ref _unitPrice, value))
                {
                    NotifyFinancialsChanged();
                }
            }
        }

        /// <summary>
        /// İşçilik Maliyeti (Bu kalem için toplam işçilik)
        /// </summary>
        public decimal LaborCost
        {
            get => _laborCost;
            set
            {
                if (SetProperty(ref _laborCost, value))
                {
                    NotifyFinancialsChanged();
                }
            }
        }

        /// <summary>
        /// Opsiyonel kalem mi? (Müşteriye opsiyonel sunulan)
        /// </summary>
        public bool IsOptional
        {
            get => _isOptional;
            set => SetProperty(ref _isOptional, value);
        }

        /// <summary>
        /// Not
        /// </summary>
        public string Note
        {
            get => _note;
            set => SetProperty(ref _note, value);
        }

        #endregion

        #region Calculated Properties (Not Serialized)

        /// <summary>
        /// Toplam Satış Fiyatı (Adet × Birim Satış)
        /// </summary>
        [JsonIgnore]
        public decimal TotalPrice => Quantity * UnitPrice;

        /// <summary>
        /// Toplam Maliyet (Adet × Birim Alış + İşçilik)
        /// </summary>
        [JsonIgnore]
        public decimal TotalCost => (Quantity * UnitCost) + LaborCost;

        /// <summary>
        /// Kar (Satış - Maliyet)
        /// </summary>
        [JsonIgnore]
        public decimal Profit => TotalPrice - TotalCost;

        /// <summary>
        /// Kar Marjı % (Kar / Satış × 100)
        /// </summary>
        [JsonIgnore]
        public decimal MarginPercent => TotalPrice > 0 ? (Profit / TotalPrice) * 100 : 0;

        /// <summary>
        /// Kar marjı gösterimi
        /// </summary>
        [JsonIgnore]
        public string MarginDisplay => $"%{MarginPercent:N1}";

        /// <summary>
        /// Kar rengi (yeşil/kırmızı)
        /// </summary>
        [JsonIgnore]
        public string ProfitColor => Profit >= 0 ? "#4CAF50" : "#F44336";

        #endregion

        #region Events

        /// <summary>
        /// Bir kalem değiştiğinde parent node'u bilgilendirmek için event
        /// </summary>
        public event Action? OnItemChanged;

        #endregion

        #region Methods

        /// <summary>
        /// Finansal değişiklikleri bildir
        /// </summary>
        private void NotifyFinancialsChanged()
        {
            OnPropertyChanged(nameof(TotalPrice));
            OnPropertyChanged(nameof(TotalCost));
            OnPropertyChanged(nameof(Profit));
            OnPropertyChanged(nameof(MarginPercent));
            OnPropertyChanged(nameof(MarginDisplay));
            OnPropertyChanged(nameof(ProfitColor));
            OnItemChanged?.Invoke();
        }

        /// <summary>
        /// Kalemi klonla
        /// </summary>
        public ScopeNodeItem Clone()
        {
            return new ScopeNodeItem
            {
                ProductId = this.ProductId,
                ProductName = this.ProductName,
                ProductSKU = this.ProductSKU,
                Quantity = this.Quantity,
                UnitCost = this.UnitCost,
                UnitPrice = this.UnitPrice,
                LaborCost = this.LaborCost,
                IsOptional = this.IsOptional,
                Note = this.Note
            };
        }

        /// <summary>
        /// Product entity'sinden oluştur
        /// </summary>
        public static ScopeNodeItem FromProduct(Product product, int quantity = 1)
        {
            return new ScopeNodeItem
            {
                ProductId = product.Id,
                ProductName = product.ProductName,
                ProductSKU = product.SKU,
                Unit = product.Unit,
                Quantity = quantity,
                UnitCost = product.PurchasePrice,
                UnitPrice = product.SalePrice
            };
        }

        #endregion
    }
}
