using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.DTOs.Customers
{
    /// <summary>
    /// Müşteri oluşturma ve güncelleme formu DTO'su.
    /// Create ve Update işlemleri aynı veri yapısını paylaşır;
    /// Id > 0 ise güncelleme, Id == 0 ise oluşturma olarak yorumlanır.
    /// </summary>
    public class CustomerCreateUpdateDto
    {
        public int Id { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string City { get; set; } = string.Empty;
        public string? District { get; set; }
        public string? Neighborhood { get; set; }
        public string? Street { get; set; }
        public string? BuildingNo { get; set; }
        public string? ApartmentNo { get; set; }
        public string? Notes { get; set; }
        public string? TcKimlikNo { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? CompanyName { get; set; }
        public string? TaxNumber { get; set; }
        public string? TaxOffice { get; set; }
        public CustomerType Type { get; set; } = CustomerType.Individual;
        public CustomerSegment Segment { get; set; } = CustomerSegment.None;
        public DateTime? BirthDate { get; set; }
        public string? Tags { get; set; }
    }
}
