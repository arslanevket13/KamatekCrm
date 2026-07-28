using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.DTOs.Customers
{
    /// <summary>
    /// Müşteri listeleme (DataGrid / tablo) için hafifletilmiş DTO.
    /// Navigasyon koleksiyonları veya ağır alanlar taşımaz.
    /// </summary>
    public class CustomerListItemDto
    {
        public int Id { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string City { get; set; } = string.Empty;
        public string? District { get; set; }
        public CustomerType Type { get; set; }
        public CustomerSegment Segment { get; set; }
        public string LoyaltyLevel { get; set; } = string.Empty;
        public decimal TotalSpent { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
