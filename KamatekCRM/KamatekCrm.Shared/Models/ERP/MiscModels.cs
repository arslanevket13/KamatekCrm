using System;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
    public class CustomerAsset
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public JobCategory Category { get; set; } = JobCategory.Other;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string? Location { get; set; }
        public AssetStatus Status { get; set; } = AssetStatus.Active;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string FullName => $"{Brand} {Model}";
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer Customer { get; set; } = null!;
    }

    public class MaintenanceContract
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime NextDueDate { get; set; }
        public int CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer Customer { get; set; } = null!;
        public string JobDescriptionTemplate { get; set; } = "";
        public decimal PricePerVisit { get; set; }
        public int FrequencyInMonths { get; set; }
    }

    public class Attachment
    {
        public int Id { get; set; }
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string Path { get; set; } = "";
        public long FileSize { get; set; }
        public string ContentType { get; set; } = "";
        public DateTime UploadDate { get; set; } = DateTime.Now;
        public string UploadedBy { get; set; } = "";
        public string Description { get; set; } = "";
        public AttachmentEntityType EntityType { get; set; }
        public int EntityId { get; set; }
    }

    public class ActivityLog
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string? Username { get; set; }
        public string ActionType { get; set; } = "";
        public string Action { get; set; } = "";
        public string? EntityName { get; set; }
        public string? RecordId { get; set; }
        public string? Description { get; set; }
        public string? AdditionalData { get; set; }
        public DateTime Timestamp { get; set; }
        public string? ReferenceId { get; set; }
        public long DurationMs { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }

    public class CategorySelectItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsSelected { get; set; }
        public JobCategory Category { get; set; }
        public string DisplayName => Category.ToString();
    }

    public class JobDetailBase { public int Id { get; set; } }

    // Address lookup models
    public class City { public int Id { get; set; } public string Name { get; set; } = ""; public virtual System.Collections.Generic.ICollection<District> Districts { get; set; } = new System.Collections.Generic.List<District>(); }
    public class District { public int Id { get; set; } public string Name { get; set; } = ""; public int CityId { get; set; } public virtual System.Collections.Generic.ICollection<Neighborhood> Neighborhoods { get; set; } = new System.Collections.Generic.List<Neighborhood>(); }
    public class Neighborhood { public int Id { get; set; } public string Name { get; set; } = ""; public int DistrictId { get; set; } }
}
