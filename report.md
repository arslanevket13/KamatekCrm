### C:\Antigravity\KamatekCRM\KamatekCrm.Shared\Models\ReportModels.cs (Satır 64)
- **Eski Kod:** `public DateTime GeneratedAt { get; set; } = DateTime.Now;`
- **Yeni Kod (UTC):** `public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;`

### C:\Antigravity\KamatekCRM\KamatekCrm.Shared\Models\ServiceJobHistory.cs (Satır 16)
- **Eski Kod:** `public DateTime Date { get; set; } = DateTime.Now;`
- **Yeni Kod (UTC):** `public DateTime Date { get; set; } = DateTime.UtcNow;`

### C:\Antigravity\KamatekCRM\KamatekCrm.Shared\Models\ServiceJobHistory.cs (Satır 38)
- **Eski Kod:** `public DateTime PerformedAt { get; set; } = DateTime.Now;`
- **Yeni Kod (UTC):** `public DateTime PerformedAt { get; set; } = DateTime.UtcNow;`

### C:\Antigravity\KamatekCRM\KamatekCrm.Shared\Models\ERP\InventoryModels.cs (Satır 80)
- **Eski Kod:** `public DateTime Date { get; set; } = DateTime.Now;`
- **Yeni Kod (UTC):** `public DateTime Date { get; set; } = DateTime.UtcNow;`

### C:\Antigravity\KamatekCRM\KamatekCrm.Shared\Models\ERP\InventoryModels.cs (Satır 115)
- **Eski Kod:** `public class ProductSerial
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = "";
        public int ProductId { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime? ManufactureDate { get; set; }
        public string? Location { get; set; }
    }

    public class InventoryImage
    {
        public int Id { get; set; }
        public int InventoryId { get; set; }
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public string UploadedBy { get; set; } = string.Empty;
        [ForeignKey(nameof(ProductId))]
        public virtual Product? Product { get; set; }
        [ForeignKey(nameof(WarehouseId))]
        public virtual Warehouse Warehouse { get; set; } = null!;
    }

    public class StockReservation
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public int Quantity { get; set; }
        public string ReferenceType { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
        public DateTime ReservedAt { get; set; } = DateTime.Now;
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        public string ReservedBy { get; set; } = string.Empty;
    }
}`
- **Yeni Kod (UTC):** `public class ProductSerial
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = "";
        public int ProductId { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime? ManufactureDate { get; set; }
        public string? Location { get; set; }
    }

    public class InventoryImage
    {
        public int Id { get; set; }
        public int InventoryId { get; set; }
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public string UploadedBy { get; set; } = string.Empty;
        [ForeignKey(nameof(ProductId))]
        public virtual Product? Product { get; set; }
        [ForeignKey(nameof(WarehouseId))]
        public virtual Warehouse Warehouse { get; set; } = null!;
    }

    public class StockReservation
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public int Quantity { get; set; }
        public string ReferenceType { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
        public DateTime ReservedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        public string ReservedBy { get; set; } = string.Empty;
    }
}`

### C:\Antigravity\KamatekCRM\KamatekCrm.Shared\Models\ERP\MiscModels.cs (Satır 18)
- **Eski Kod:** `public DateTime CreatedDate { get; set; } = DateTime.Now;`
- **Yeni Kod (UTC):** `public DateTime CreatedDate { get; set; } = DateTime.UtcNow;`

### C:\Antigravity\KamatekCRM\KamatekCrm.Shared\Models\ERP\MiscModels.cs (Satır 46)
- **Eski Kod:** `public DateTime UploadDate { get; set; } = DateTime.Now;`
- **Yeni Kod (UTC):** `public DateTime UploadDate { get; set; } = DateTime.UtcNow;`

### C:\Antigravity\KamatekCRM\KamatekCrm.Shared\Models\ERP\SalesModels.cs (Satır 7)
- **Eski Kod:** `public class SalesOrder
    {
        public int Id { get; set; }
        public int? CustomerId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.Now;
        public string PaymentMethod { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal TotalAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
        public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Completed;
        
        /// <summary>
        /// Fiş tekrar yazdırıldı mı?
        /// </summary>
        public bool IsReprinted { get; set; }
        
        /// <summary>
        /// Kaç kez yazdırıldı
        /// </summary>
        public int PrintCount { get; set; }
        
        public virtual Customer? Customer { get; set; }
        public virtual System.Collections.Generic.ICollection<SalesOrderItem> Items { get; set; } = new System.Collections.Generic.List<SalesOrderItem>();
        public virtual System.Collections.Generic.ICollection<SalesOrderPayment> Payments { get; set; } = new System.Collections.Generic.List<SalesOrderPayment>();
    }`
- **Yeni Kod (UTC):** `public class SalesOrder
    {
        public int Id { get; set; }
        public int? CustomerId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string PaymentMethod { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal TotalAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
        public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Completed;
        
        /// <summary>
        /// Fiş tekrar yazdırıldı mı?
        /// </summary>
        public bool IsReprinted { get; set; }
        
        /// <summary>
        /// Kaç kez yazdırıldı
        /// </summary>
        public int PrintCount { get; set; }
        
        public virtual Customer? Customer { get; set; }
        public virtual System.Collections.Generic.ICollection<SalesOrderItem> Items { get; set; } = new System.Collections.Generic.List<SalesOrderItem>();
        public virtual System.Collections.Generic.ICollection<SalesOrderPayment> Payments { get; set; } = new System.Collections.Generic.List<SalesOrderPayment>();
    }`

### C:\Antigravity\KamatekCRM\KamatekCrm.Shared\Models\ERP\SalesModels.cs (Satır 53)
- **Eski Kod:** `public DateTime Date { get; set; } = DateTime.Now;`
- **Yeni Kod (UTC):** `public DateTime Date { get; set; } = DateTime.UtcNow;`

### C:\Antigravity\KamatekCRM\KamatekCrm.Shared\Models\ERP\SalesModels.cs (Satır 61)
- **Eski Kod:** `public DateTime CreatedAt { get; set; } = DateTime.Now;`
- **Yeni Kod (UTC):** `public DateTime CreatedAt { get; set; } = DateTime.UtcNow;`

### C:\Antigravity\KamatekCRM\KamatekCrm.Shared\Models\ERP\SupplierModels.cs (Satır 23)
- **Eski Kod:** `public DateTime Date { get; set; } = DateTime.Now;`
- **Yeni Kod (UTC):** `public DateTime Date { get; set; } = DateTime.UtcNow;`

### C:\Antigravity\KamatekCRM\KamatekCrm.Shared\Models\ERP\SupplierModels.cs (Satır 24)
- **Eski Kod:** `public DateTime OrderDate { get; set; } = DateTime.Now;`
- **Yeni Kod (UTC):** `public DateTime OrderDate { get; set; } = DateTime.UtcNow;`

### C:\Antigravity\KamatekCRM\ViewModels\AddProductViewModel.cs (Satır 316)
- **Eski Kod:** `return $"PRD-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";`
- **Yeni Kod (UTC):** `return $"PRD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";`

### C:\Antigravity\KamatekCRM\ViewModels\AddProductViewModel.cs (Satır 452)
- **Eski Kod:** `Date = DateTime.Now,`
- **Yeni Kod (UTC):** `Date = DateTime.UtcNow,`

### C:\Antigravity\KamatekCRM\ViewModels\AddProductViewModel.cs (Satır 521)
- **Eski Kod:** `Date = DateTime.Now,`
- **Yeni Kod (UTC):** `Date = DateTime.UtcNow,`

### C:\Antigravity\KamatekCRM\ViewModels\AnalyticsViewModel.cs (Satır 159)
- **Eski Kod:** `.Select(i => DateTime.Now.AddMonths(-i))`
- **Yeni Kod (UTC):** `.Select(i => DateTime.UtcNow.AddMonths(-i))`

### C:\Antigravity\KamatekCRM\ViewModels\CustomerAddViewModel.cs (Satır 335)
- **Eski Kod:** `int year = DateTime.Now.Year;`
- **Yeni Kod (UTC):** `int year = DateTime.UtcNow.Year;`

### C:\Antigravity\KamatekCRM\ViewModels\CustomerDetailViewModel.cs (Satır 192)
- **Eski Kod:** `/// <summary>
        /// Aktif iş sayısı
        /// </summary>
        public int ActiveJobCount => ActiveJobs?.Count ?? 0;

        // Yeni Alanlar için Property'ler
        public string? Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        public CustomerSegment Segment
        {
            get => _segment;
            set => SetProperty(ref _segment, value);
        }

        public DateTime? BirthDate
        {
            get => _birthDate;
            set => SetProperty(ref _birthDate, value);
        }

        public string? LoyaltyLevel
        {
            get => _loyaltyLevel;
            private set => SetProperty(ref _loyaltyLevel, value);
        }

        /// <summary>
        /// Doğum günü yaklaşıyor mu?
        /// </summary>
        public bool HasUpcomingBirthday
        {
            get
            {
                if (!BirthDate.HasValue) return false;
                var today = DateTime.Today;
                var bday = BirthDate.Value;
                var thisYearBirthday = new DateTime(today.Year, bday.Month, bday.Day);
                var daysUntil = (thisYearBirthday - today).Days;
                return daysUntil >= 0 && daysUntil <= 30;
            }
        }

        #endregion`
- **Yeni Kod (UTC):** `/// <summary>
        /// Aktif iş sayısı
        /// </summary>
        public int ActiveJobCount => ActiveJobs?.Count ?? 0;

        // Yeni Alanlar için Property'ler
        public string? Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        public CustomerSegment Segment
        {
            get => _segment;
            set => SetProperty(ref _segment, value);
        }

        public DateTime? BirthDate
        {
            get => _birthDate;
            set => SetProperty(ref _birthDate, value);
        }

        public string? LoyaltyLevel
        {
            get => _loyaltyLevel;
            private set => SetProperty(ref _loyaltyLevel, value);
        }

        /// <summary>
        /// Doğum günü yaklaşıyor mu?
        /// </summary>
        public bool HasUpcomingBirthday
        {
            get
            {
                if (!BirthDate.HasValue) return false;
                var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
                var bday = BirthDate.Value;
                var thisYearBirthday = new DateTime(today.Year, bday.Month, bday.Day, 0, 0, 0, DateTimeKind.Utc);
                var daysUntil = (thisYearBirthday - today).Days;
                return daysUntil >= 0 && daysUntil <= 30;
            }
        }

        #endregion`

### C:\Antigravity\KamatekCRM\ViewModels\CustomerDetailViewModel.cs (Satır 330)
- **Eski Kod:** `Date = DateTime.Now,`
- **Yeni Kod (UTC):** `Date = DateTime.UtcNow,`

### C:\Antigravity\KamatekCRM\ViewModels\CustomerDetailViewModel.cs (Satır 404)
- **Eski Kod:** `// Müşteriyi önceden seç
            if (_customer != null)
            {
                serviceJobViewModel.SelectedCustomer = _customer;
            }
        }

        private void AddNote()
        {
            var note = Notes;
            if (string.IsNullOrWhiteSpace(note))
            {
                MessageBox.Show("Lütfen bir not girin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var activity = new CustomerActivity
                {
                    CustomerId = _customerId,
                    Type = ActivityType.NoteAdded,
                    Description = note,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "Kullanıcı"
                };

                _context.CustomerActivities.Add(activity);
                _context.SaveChanges();

                // Activity'yi listeye ekle
                Activities.Insert(0, activity);

                // Müşterinin notlarını da güncelle
                if (_customer != null)
                {
                    _customer.Notes = string.IsNullOrEmpty(_customer.Notes) 
                        ? note 
                        : _customer.Notes + "\n" + DateTime.Now.ToString("dd.MM.yyyy") + ": " + note;
                    _customer.LastInteractionDate = DateTime.UtcNow;
                    _context.SaveChanges();
                }

                Notes = string.Empty;
                _toastService.ShowSuccess("Not başarıyla eklendi!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Not ekleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveTags()
        {
            if (_customer == null) return;

            try
            {
                _customer.Tags = Tags;
                _customer.LastInteractionDate = DateTime.UtcNow;
                _context.SaveChanges();

                // Etiket ekleme aktivitesi kaydet
                var activity = new CustomerActivity
                {
                    CustomerId = _customerId,
                    Type = ActivityType.TagAdded,
                    Description = $"Etiketler güncellendi: {Tags}",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "Kullanıcı"
                };
                _context.CustomerActivities.Add(activity);
                _context.SaveChanges();

                Activities.Insert(0, activity);
                _toastService.ShowSuccess("Etiketler kaydedildi!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Etiket kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSegment()
        {
            if (_customer == null) return;

            try
            {
                _customer.Segment = Segment;
                _customer.LastInteractionDate = DateTime.UtcNow;
                _context.SaveChanges();

                _toastService.ShowSuccess($"Müşteri segmenti '{Segment}' olarak güncellendi!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Segment güncelleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}`
- **Yeni Kod (UTC):** `// Müşteriyi önceden seç
            if (_customer != null)
            {
                serviceJobViewModel.SelectedCustomer = _customer;
            }
        }

        private void AddNote()
        {
            var note = Notes;
            if (string.IsNullOrWhiteSpace(note))
            {
                MessageBox.Show("Lütfen bir not girin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var activity = new CustomerActivity
                {
                    CustomerId = _customerId,
                    Type = ActivityType.NoteAdded,
                    Description = note,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "Kullanıcı"
                };

                _context.CustomerActivities.Add(activity);
                _context.SaveChanges();

                // Activity'yi listeye ekle
                Activities.Insert(0, activity);

                // Müşterinin notlarını da güncelle
                if (_customer != null)
                {
                    _customer.Notes = string.IsNullOrEmpty(_customer.Notes) 
                        ? note 
                        : _customer.Notes + "\n" + DateTime.UtcNow.ToString("dd.MM.yyyy") + ": " + note;
                    _customer.LastInteractionDate = DateTime.UtcNow;
                    _context.SaveChanges();
                }

                Notes = string.Empty;
                _toastService.ShowSuccess("Not başarıyla eklendi!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Not ekleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveTags()
        {
            if (_customer == null) return;

            try
            {
                _customer.Tags = Tags;
                _customer.LastInteractionDate = DateTime.UtcNow;
                _context.SaveChanges();

                // Etiket ekleme aktivitesi kaydet
                var activity = new CustomerActivity
                {
                    CustomerId = _customerId,
                    Type = ActivityType.TagAdded,
                    Description = $"Etiketler güncellendi: {Tags}",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "Kullanıcı"
                };
                _context.CustomerActivities.Add(activity);
                _context.SaveChanges();

                Activities.Insert(0, activity);
                _toastService.ShowSuccess("Etiketler kaydedildi!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Etiket kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSegment()
        {
            if (_customer == null) return;

            try
            {
                _customer.Segment = Segment;
                _customer.LastInteractionDate = DateTime.UtcNow;
                _context.SaveChanges();

                _toastService.ShowSuccess($"Müşteri segmenti '{Segment}' olarak güncellendi!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Segment güncelleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}`

### C:\Antigravity\KamatekCRM\ViewModels\DashboardViewModel.cs (Satır 38)
- **Eski Kod:** `public string TodayDate => DateTime.Now.ToString("dd MMMM yyyy, dddd", new System.Globalization.CultureInfo("tr-TR"));`
- **Yeni Kod (UTC):** `public string TodayDate => DateTime.UtcNow.ToString("dd MMMM yyyy, dddd", new System.Globalization.CultureInfo("tr-TR"));`

### C:\Antigravity\KamatekCRM\ViewModels\DashboardViewModel.cs (Satır 43)
- **Eski Kod:** `public string CurrentMonthName => DateTime.Now.ToString("MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));`
- **Yeni Kod (UTC):** `public string CurrentMonthName => DateTime.UtcNow.ToString("MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));`

### C:\Antigravity\KamatekCRM\ViewModels\FieldJobListViewModel.cs (Satır 254)
- **Eski Kod:** `dbJob.CompletedDate = DateTime.Now;`
- **Yeni Kod (UTC):** `dbJob.CompletedDate = DateTime.UtcNow;`

### C:\Antigravity\KamatekCRM\ViewModels\FinanceViewModel.cs (Satır 31)
- **Eski Kod:** `private DateTime _selectedDate = DateTime.Today;`
- **Yeni Kod (UTC):** `private DateTime _selectedDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);`

### C:\Antigravity\KamatekCRM\ViewModels\FinanceViewModel.cs (Satır 168)
- **Eski Kod:** `GoToTodayCommand = new RelayCommand(_ => SelectedDate = DateTime.Today);`
- **Yeni Kod (UTC):** `GoToTodayCommand = new RelayCommand(_ => SelectedDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc));`

### C:\Antigravity\KamatekCRM\ViewModels\FinanceViewModel.cs (Satır 188)
- **Eski Kod:** `startDate = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);`
- **Yeni Kod (UTC):** `startDate = new DateTime(SelectedDate.Year, SelectedDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);`

### C:\Antigravity\KamatekCRM\ViewModels\FinanceViewModel.cs (Satır 262)
- **Eski Kod:** `Date = DateTime.Now,`
- **Yeni Kod (UTC):** `Date = DateTime.UtcNow,`

### C:\Antigravity\KamatekCRM\ViewModels\FinanceViewModel.cs (Satır 268)
- **Eski Kod:** `CreatedAt = DateTime.Now`
- **Yeni Kod (UTC):** `CreatedAt = DateTime.UtcNow`

### C:\Antigravity\KamatekCRM\ViewModels\FinancialHealthViewModel.cs (Satır 76)
- **Eski Kod:** `.Select(i => DateTime.Now.AddMonths(-i))`
- **Yeni Kod (UTC):** `.Select(i => DateTime.UtcNow.AddMonths(-i))`

### C:\Antigravity\KamatekCRM\ViewModels\ProductViewModel.cs (Satır 347)
- **Eski Kod:** `SKU = sku ?? $"IMP-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",`
- **Yeni Kod (UTC):** `SKU = sku ?? $"IMP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",`

### C:\Antigravity\KamatekCRM\ViewModels\ProductViewModel.cs (Satır 394)
- **Eski Kod:** `Date = DateTime.Now,`
- **Yeni Kod (UTC):** `Date = DateTime.UtcNow,`

### C:\Antigravity\KamatekCRM\ViewModels\ProjectQuoteEditorViewModel.cs (Satır 739)
- **Eski Kod:** `FileName = $"Teklif_{ProjectName}_{DateTime.Now:yyyyMMdd}",`
- **Yeni Kod (UTC):** `FileName = $"Teklif_{ProjectName}_{DateTime.UtcNow:yyyyMMdd}",`

### C:\Antigravity\KamatekCRM\ViewModels\ProjectQuoteEditorViewModel.cs (Satır 814)
- **Eski Kod:** `string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Teklif_{ProjectName}_{DateTime.Now:yyyyMMddHHmmss}.pdf");`
- **Yeni Kod (UTC):** `string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Teklif_{ProjectName}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");`

### C:\Antigravity\KamatekCRM\ViewModels\ProjectQuoteViewModel.cs (Satır 206)
- **Eski Kod:** `var year = DateTime.Now.Year;`
- **Yeni Kod (UTC):** `var year = DateTime.UtcNow.Year;`

### C:\Antigravity\KamatekCRM\ViewModels\ProjectQuoteViewModel.cs (Satır 215)
- **Eski Kod:** `CreatedDate = DateTime.Now,`
- **Yeni Kod (UTC):** `CreatedDate = DateTime.UtcNow,`

### C:\Antigravity\KamatekCRM\ViewModels\PurchaseOrderViewModel.cs (Satır 327)
- **Eski Kod:** `OrderDate = DateTime.Now,`
- **Yeni Kod (UTC):** `OrderDate = DateTime.UtcNow,`

### C:\Antigravity\KamatekCRM\ViewModels\PurchaseOrderViewModel.cs (Satır 328)
- **Eski Kod:** `Date = DateTime.Now,`
- **Yeni Kod (UTC):** `Date = DateTime.UtcNow,`

### C:\Antigravity\KamatekCRM\ViewModels\PurchaseOrderViewModel.cs (Satır 330)
- **Eski Kod:** `InvoiceNumber = $"INV-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",`
- **Yeni Kod (UTC):** `InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",`

### C:\Antigravity\KamatekCRM\ViewModels\QuickAssetAddViewModel.cs (Satır 123)
- **Eski Kod:** `CreatedDate = DateTime.Now`
- **Yeni Kod (UTC):** `CreatedDate = DateTime.UtcNow`

### C:\Antigravity\KamatekCRM\ViewModels\QuickNewProductForPurchaseViewModel.cs (Satır 166)
- **Eski Kod:** `? $"POI-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}"`
- **Yeni Kod (UTC):** `? $"POI-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}"`

### C:\Antigravity\KamatekCRM\ViewModels\QuoteListViewModel.cs (Satır 350)
- **Eski Kod:** `FileName = $"Teklif_{SelectedQuote.Title}_{DateTime.Now:yyyyMMdd}",`
- **Yeni Kod (UTC):** `FileName = $"Teklif_{SelectedQuote.Title}_{DateTime.UtcNow:yyyyMMdd}",`

### C:\Antigravity\KamatekCRM\ViewModels\RepairListViewModel.cs (Satır 737)
- **Eski Kod:** `public int DaysInShop => (DateTime.Now - CreatedDate).Days;`
- **Yeni Kod (UTC):** `public int DaysInShop => (DateTime.UtcNow - CreatedDate).Days;`

### C:\Antigravity\KamatekCRM\ViewModels\RepairViewModel.cs (Satır 432)
- **Eski Kod:** `CreatedDate = DateTime.Now,`
- **Yeni Kod (UTC):** `CreatedDate = DateTime.UtcNow,`

### C:\Antigravity\KamatekCRM\ViewModels\RoutePlanningViewModel.cs (Satır 63)
- **Eski Kod:** `private DateTime _selectedDate = DateTime.Today;`
- **Yeni Kod (UTC):** `private DateTime _selectedDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);`

### C:\Antigravity\KamatekCRM\ViewModels\StockCountViewModel.cs (Satır 28)
- **Eski Kod:** `private DateTime _countDate = DateTime.Today;`
- **Yeni Kod (UTC):** `private DateTime _countDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);`

### C:\Antigravity\KamatekCRM\ViewModels\StockCountViewModel.cs (Satır 425)
- **Eski Kod:** `FileName = $"StokSayim_{SelectedWarehouse?.Name ?? "Tum"}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",`
- **Yeni Kod (UTC):** `FileName = $"StokSayim_{SelectedWarehouse?.Name ?? "Tum"}_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx",`

### C:\Antigravity\KamatekCRM\ViewModels\StockCountViewModel.cs (Satır 444)
- **Eski Kod:** `worksheet.Cell(4, 1).Value = $"Rapor Oluşturma: {DateTime.Now:dd.MM.yyyy HH:mm}";`
- **Yeni Kod (UTC):** `worksheet.Cell(4, 1).Value = $"Rapor Oluşturma: {DateTime.UtcNow:dd.MM.yyyy HH:mm}";`

### C:\Antigravity\KamatekCRM\ViewModels\StockCountViewModel.cs (Satır 896)
- **Eski Kod:** `var referenceId = $"MANUAL-{DateTime.Now:yyyyMMdd-HHmmss}-{ManualSelectedWarehouse.Id}";`
- **Yeni Kod (UTC):** `var referenceId = $"MANUAL-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{ManualSelectedWarehouse.Id}";`

### C:\Antigravity\KamatekCRM\ViewModels\StockCountViewModel.cs (Satır 923)
- **Eski Kod:** `Date = DateTime.Now,`
- **Yeni Kod (UTC):** `Date = DateTime.UtcNow,`

### C:\Antigravity\KamatekCRM\ViewModels\StockReportsViewModel.cs (Satır 136)
- **Eski Kod:** `StartDate = DateTime.Today.AddDays(-30);`
- **Yeni Kod (UTC):** `StartDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc).AddDays(-30);`

### C:\Antigravity\KamatekCRM\ViewModels\StockReportsViewModel.cs (Satır 137)
- **Eski Kod:** `EndDate = DateTime.Today;`
- **Yeni Kod (UTC):** `EndDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);`

### C:\Antigravity\KamatekCRM\ViewModels\StockReportsViewModel.cs (Satır 284)
- **Eski Kod:** `StartDate = DateTime.Today.AddDays(-30);`
- **Yeni Kod (UTC):** `StartDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc).AddDays(-30);`

### C:\Antigravity\KamatekCRM\ViewModels\StockReportsViewModel.cs (Satır 285)
- **Eski Kod:** `EndDate = DateTime.Today;`
- **Yeni Kod (UTC):** `EndDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);`

### C:\Antigravity\KamatekCRM\ViewModels\SystemLogsViewModel.cs (Satır 162)
- **Eski Kod:** `_startDate = DateTime.Today.AddDays(-7);`
- **Yeni Kod (UTC):** `_startDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc).AddDays(-7);`

### C:\Antigravity\KamatekCRM\ViewModels\SystemLogsViewModel.cs (Satır 163)
- **Eski Kod:** `_endDate = DateTime.Today.AddDays(1);`
- **Yeni Kod (UTC):** `_endDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc).AddDays(1);`

### C:\Antigravity\KamatekCRM\ViewModels\SystemLogsViewModel.cs (Satır 233)
- **Eski Kod:** `_startDate = DateTime.Today.AddDays(-7);`
- **Yeni Kod (UTC):** `_startDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc).AddDays(-7);`

### C:\Antigravity\KamatekCRM\ViewModels\SystemLogsViewModel.cs (Satır 234)
- **Eski Kod:** `_endDate = DateTime.Today.AddDays(1);`
- **Yeni Kod (UTC):** `_endDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc).AddDays(1);`

### C:\Antigravity\KamatekCrm.API\Controllers\DashboardController.cs (Satır 77)
- **Eski Kod:** `var startOfMonth = new DateTime(today.Year, today.Month, 1);`
- **Yeni Kod (UTC):** `var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);`

### C:\Antigravity\KamatekCrm.API\Controllers\DashboardController.cs (Satır 179)
- **Eski Kod:** `var thisYearBirthday = new DateTime(today.Year, bday.Month, bday.Day);`
- **Yeni Kod (UTC):** `var thisYearBirthday = new DateTime(today.Year, bday.Month, bday.Day, 0, 0, 0, DateTimeKind.Utc);`

### C:\Antigravity\KamatekCrm.API\Controllers\DashboardController.cs (Satır 187)
- **Eski Kod:** `var thisYear = new DateTime(today.Year, bday.Month, bday.Day);`
- **Yeni Kod (UTC):** `var thisYear = new DateTime(today.Year, bday.Month, bday.Day, 0, 0, 0, DateTimeKind.Utc);`

### C:\Antigravity\KamatekCrm.API\Controllers\PhotoController.cs (Satır 101)
- **Eski Kod:** `UploadedAt = DateTime.Now`
- **Yeni Kod (UTC):** `UploadedAt = DateTime.UtcNow`

### C:\Antigravity\KamatekCrm.API\Controllers\PhotoController.cs (Satır 131)
- **Eski Kod:** `photo.DeletedAt = DateTime.Now;`
- **Yeni Kod (UTC):** `photo.DeletedAt = DateTime.UtcNow;`

### C:\Antigravity\KamatekCrm.API\Controllers\ReportsController.cs (Satır 164)
- **Eski Kod:** `&& j.SlaDeadline.Value < DateTime.Now.AddHours(8)`
- **Yeni Kod (UTC):** `&& j.SlaDeadline.Value < DateTime.UtcNow.AddHours(8)`

### C:\Antigravity\KamatekCrm.API\Services\PdfReportService.cs (Satır 350)
- **Eski Kod:** `col.Item().Text($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(8).FontColor(SecondaryColor);`
- **Yeni Kod (UTC):** `col.Item().Text($"Tarih: {DateTime.UtcNow:dd.MM.yyyy HH:mm}").FontSize(8).FontColor(SecondaryColor);`

### C:\Antigravity\KamatekCrm.Web\Features\Route\RouteEndpoints.cs (Satır 24)
- **Eski Kod:** `var targetDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);`
- **Yeni Kod (UTC):** `var targetDate = string.IsNullOrEmpty(date) ? DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc) : DateTime.Parse(date);`

### C:\Antigravity\KamatekCrm.Web\Features\Technician\JobWorkflowEndpoints.cs (Satır 111)
- **Eski Kod:** `var now = DateTime.Now;`
- **Yeni Kod (UTC):** `var now = DateTime.UtcNow;`

### C:\Antigravity\KamatekCrm.Web\Features\Technician\TechnicianDashboardEndpoints.cs (Satır 34)
- **Eski Kod:** `var today = DateTime.Today.ToString("yyyy-MM-dd");`
- **Yeni Kod (UTC):** `var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc).ToString("yyyy-MM-dd");`

### C:\Antigravity\KamatekCrm.Web\Features\Technician\TechnicianDashboardEndpoints.cs (Satır 72)
- **Eski Kod:** `var targetDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);`
- **Yeni Kod (UTC):** `var targetDate = string.IsNullOrEmpty(date) ? DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc) : DateTime.Parse(date);`

### C:\Antigravity\KamatekCrm.Web\Shared\HtmlTemplates.cs (Satır 182)
- **Eski Kod:** `var now = DateTime.Now;`
- **Yeni Kod (UTC):** `var now = DateTime.UtcNow;`

### C:\Antigravity\KamatekCrm.Web\Shared\HtmlTemplates.cs (Satır 588)
- **Eski Kod:** `var now = DateTime.Now;`
- **Yeni Kod (UTC):** `var now = DateTime.UtcNow;`

### C:\Antigravity\KamatekCrm.Web\Shared\HtmlTemplates.cs (Satır 693)
- **Eski Kod:** `var isToday = date.Date == DateTime.Today;`
- **Yeni Kod (UTC):** `var isToday = date.Date == DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);`

### C:\Antigravity\KamatekCrm.Web\Shared\HtmlTemplates.cs (Satır 814)
- **Eski Kod:** `var isToday = date.Date == DateTime.Today;`
- **Yeni Kod (UTC):** `var isToday = date.Date == DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);`


