namespace KamatekCrm.ApplicationCore.DTOs.Users
{
    /// <summary>
    /// Kullanıcı oluşturma ve güncelleme formu DTO'su.
    /// Password alanı yalnızca oluşturma veya şifre değiştirme senaryosunda doldurulur.
    /// </summary>
    public class UserCreateUpdateDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Yalnızca oluşturma veya şifre sıfırlama sırasında doldurulur.
        /// Güncelleme sırasında null/empty bırakılırsa mevcut hash korunur.
        /// </summary>
        public string? Password { get; set; }

        public string Ad { get; set; } = string.Empty;
        public string Soyad { get; set; } = string.Empty;
        public string Role { get; set; } = "Viewer";
        public bool IsActive { get; set; } = true;
        public bool IsTechnician { get; set; }
        public string? Phone { get; set; }
        public string? VehiclePlate { get; set; }
        public string? ServiceArea { get; set; }
        public string? ExpertiseAreas { get; set; }

        // RBAC İzinleri
        public bool CanViewFinance { get; set; }
        public bool CanViewAnalytics { get; set; }
        public bool CanDeleteRecords { get; set; }
        public bool CanApprovePurchase { get; set; }
        public bool CanAccessSettings { get; set; }
    }
}
