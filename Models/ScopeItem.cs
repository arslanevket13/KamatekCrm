using System;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Mahal kalemi - Bir node'a atanmış ürün
    /// </summary>
    public class ScopeItem : ViewModelBase
    {
        private int _quantity = 1;
        private decimal _unitPrice;
        private string _note = string.Empty;

        #region Properties

        /// <summary>
        /// Benzersiz ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

        /// <summary>
        /// Ürün ID (Product tablosundan)
        /// </summary>
        public int ProductId { get; set; }

        /// <summary>
        /// Ürün adı (görüntüleme için)
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// SKU/Barkod
        /// </summary>
        public string? ProductSKU { get; set; }

        /// <summary>
        /// Adet
        /// </summary>
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                {
                    OnPropertyChanged(nameof(TotalPrice));
                }
            }
        }

        /// <summary>
        /// Birim fiyat
        /// </summary>
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetProperty(ref _unitPrice, value))
                {
                    OnPropertyChanged(nameof(TotalPrice));
                }
            }
        }

        /// <summary>
        /// Toplam fiyat
        /// </summary>
        public decimal TotalPrice => Quantity * UnitPrice;

        /// <summary>
        /// Not
        /// </summary>
        public string Note
        {
            get => _note;
            set => SetProperty(ref _note, value);
        }

        /// <summary>
        /// Üst node ID
        /// </summary>
        public string ParentNodeId { get; set; } = string.Empty;

        #endregion

        #region Methods

        /// <summary>
        /// Kopyala
        /// </summary>
        public ScopeItem Clone()
        {
            return new ScopeItem
            {
                ProductId = this.ProductId,
                ProductName = this.ProductName,
                ProductSKU = this.ProductSKU,
                Quantity = this.Quantity,
                UnitPrice = this.UnitPrice,
                Note = this.Note
            };
        }

        #endregion
    }
}
