using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.DTOs.Customers
{
    /// <summary>
    /// Müşteri detay görünümü için tam DTO.
    /// Tüm müşteri bilgilerini UI katmanına taşır ancak navigasyon koleksiyonları içermez.
    /// </summary>
    public class CustomerDetailDto
    {
        public int Id { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }

        // Adres Bilgileri
        public string City { get; set; } = string.Empty;
        public string? District { get; set; }
        public string? Neighborhood { get; set; }
        public string? Street { get; set; }
        public string? BuildingNo { get; set; }
        public string? ApartmentNo { get; set; }
        public string FullAddress { get; set; } = string.Empty;
        public string? Notes { get; set; }

        // Kimlik & Ticari Bilgiler
        public string? TcKimlikNo { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? CompanyName { get; set; }
        public string? TaxNumber { get; set; }
        public string? TaxOffice { get; set; }

        // Segment & Sadakat Bilgileri
        public CustomerType Type { get; set; }
        public CustomerSegment Segment { get; set; }
        public string LoyaltyLevel { get; set; } = string.Empty;
        public int LoyaltyPoints { get; set; }
        public decimal TotalSpent { get; set; }
        public int TotalPurchaseCount { get; set; }
        public DateTime? LastPurchaseDate { get; set; }
        public DateTime? LastInteractionDate { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? Tags { get; set; }

        // Audit Bilgileri
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
