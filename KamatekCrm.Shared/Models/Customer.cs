using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
    public class Customer : KamatekCrm.Shared.Models.Common.BaseEntity
    {
        // Id is in BaseEntity
        public CustomerType Type { get; set; } = CustomerType.Individual;
        [Required]
        [MaxLength(20)]
        public string CustomerCode { get; set; } = string.Empty;
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;
        [MaxLength(100)]
        public string? Email { get; set; }
        [Required]
        [MaxLength(50)]
        public string City { get; set; } = string.Empty;
        
        [MaxLength(50)]
        public string? District { get; set; }
        [MaxLength(100)]
        public string? Neighborhood { get; set; }
        [MaxLength(200)]
        public string? Street { get; set; }
        [MaxLength(10)]
        public string? BuildingNo { get; set; }
        [MaxLength(10)]
        public string? ApartmentNo { get; set; }
        [MaxLength(2000)]
        public string? Notes { get; set; }

        [MaxLength(11)]
        public string? TcKimlikNo { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [MaxLength(300)]
        public string? CompanyName { get; set; }
        [MaxLength(20)]
        public string? TaxNumber { get; set; }
        [MaxLength(100)]
        public string? TaxOffice { get; set; }

        public string FullAddress
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(Neighborhood)) parts.Add(Neighborhood);
                if (!string.IsNullOrWhiteSpace(Street)) parts.Add(Street);
                if (!string.IsNullOrWhiteSpace(BuildingNo)) parts.Add($"No: {BuildingNo}");
                if (!string.IsNullOrWhiteSpace(ApartmentNo)) parts.Add($"D: {ApartmentNo}");
                if (!string.IsNullOrWhiteSpace(District)) parts.Add(District);
                parts.Add(City);
                return string.Join(", ", parts);
            }
        }

        public virtual System.Collections.Generic.ICollection<ServiceJob> ServiceJobs { get; set; } = new System.Collections.Generic.List<ServiceJob>();
        public virtual System.Collections.Generic.ICollection<Transaction> Transactions { get; set; } = new System.Collections.Generic.List<Transaction>();
        
        // Missing property from original which might be used? Assets collection.
        // ApiDbContext error earlier mentioned 'Assets' missing. I removed it from ApiDbContext.
        // But WPF might use it? I stubbed CustomerAsset.
        public virtual System.Collections.Generic.ICollection<CustomerAsset> Assets { get; set; } = new System.Collections.Generic.List<CustomerAsset>();
    }
}
