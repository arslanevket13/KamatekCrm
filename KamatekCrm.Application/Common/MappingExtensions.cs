using KamatekCrm.ApplicationCore.DTOs.Customers;
using KamatekCrm.ApplicationCore.DTOs.Users;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ApplicationCore.Common
{
    /// <summary>
    /// Entity ↔ DTO dönüşüm extension metotları.
    /// AutoMapper gibi harici bir kütüphane bağımlılığından kaçınarak
    /// saf C# ile açık ve denetlenebilir eşleme sağlar.
    /// </summary>
    public static class MappingExtensions
    {
        // ======================== CUSTOMER MAPPINGS ========================

        public static CustomerListItemDto ToListItemDto(this Customer entity)
        {
            return new CustomerListItemDto
            {
                Id = entity.Id,
                CustomerCode = entity.CustomerCode,
                FullName = entity.FullName,
                CompanyName = entity.CompanyName,
                PhoneNumber = entity.PhoneNumber,
                Email = entity.Email,
                City = entity.City,
                District = entity.District,
                Type = entity.Type,
                Segment = entity.Segment,
                LoyaltyLevel = entity.LoyaltyLevel,
                TotalSpent = entity.TotalSpent,
                IsActive = !entity.IsDeleted,
                CreatedDate = entity.CreatedDate
            };
        }

        public static CustomerDetailDto ToDetailDto(this Customer entity)
        {
            return new CustomerDetailDto
            {
                Id = entity.Id,
                CustomerCode = entity.CustomerCode,
                FullName = entity.FullName,
                PhoneNumber = entity.PhoneNumber,
                Email = entity.Email,
                City = entity.City,
                District = entity.District,
                Neighborhood = entity.Neighborhood,
                Street = entity.Street,
                BuildingNo = entity.BuildingNo,
                ApartmentNo = entity.ApartmentNo,
                FullAddress = entity.FullAddress,
                Notes = entity.Notes,
                TcKimlikNo = entity.TcKimlikNo,
                Latitude = entity.Latitude,
                Longitude = entity.Longitude,
                CompanyName = entity.CompanyName,
                TaxNumber = entity.TaxNumber,
                TaxOffice = entity.TaxOffice,
                Type = entity.Type,
                Segment = entity.Segment,
                LoyaltyLevel = entity.LoyaltyLevel,
                LoyaltyPoints = entity.LoyaltyPoints,
                TotalSpent = entity.TotalSpent,
                TotalPurchaseCount = entity.TotalPurchaseCount,
                LastPurchaseDate = entity.LastPurchaseDate,
                LastInteractionDate = entity.LastInteractionDate,
                BirthDate = entity.BirthDate,
                Tags = entity.Tags,
                CreatedAt = entity.CreatedDate,
                UpdatedAt = entity.ModifiedDate
            };
        }

        public static void ApplyToEntity(this CustomerCreateUpdateDto dto, Customer entity)
        {
            entity.CustomerCode = dto.CustomerCode;
            entity.FullName = dto.FullName;
            entity.PhoneNumber = dto.PhoneNumber;
            entity.Email = dto.Email;
            entity.City = dto.City;
            entity.District = dto.District;
            entity.Neighborhood = dto.Neighborhood;
            entity.Street = dto.Street;
            entity.BuildingNo = dto.BuildingNo;
            entity.ApartmentNo = dto.ApartmentNo;
            entity.Notes = dto.Notes;
            entity.TcKimlikNo = dto.TcKimlikNo;
            entity.Latitude = dto.Latitude;
            entity.Longitude = dto.Longitude;
            entity.CompanyName = dto.CompanyName;
            entity.TaxNumber = dto.TaxNumber;
            entity.TaxOffice = dto.TaxOffice;
            entity.Type = dto.Type;
            entity.Segment = dto.Segment;
            entity.BirthDate = dto.BirthDate;
            entity.Tags = dto.Tags;
        }

        // ======================== USER MAPPINGS ========================

        public static UserListItemDto ToListItemDto(this User entity)
        {
            return new UserListItemDto
            {
                Id = entity.Id,
                Username = entity.Username,
                Ad = entity.Ad,
                Soyad = entity.Soyad,
                AdSoyad = entity.AdSoyad,
                Role = entity.Role,
                Phone = entity.Phone,
                CreatedDate = entity.CreatedDate,
                IsActive = entity.IsActive,
                IsTechnician = entity.IsTechnician,
                LastLoginDate = entity.LastLoginDate,
                ServiceArea = entity.ServiceArea
            };
        }

        public static void ApplyToEntity(this UserCreateUpdateDto dto, User entity)
        {
            entity.Username = dto.Username;
            entity.Ad = dto.Ad;
            entity.Soyad = dto.Soyad;
            entity.Role = dto.Role;
            entity.IsActive = dto.IsActive;
            entity.IsTechnician = dto.IsTechnician;
            entity.Phone = dto.Phone;
            entity.VehiclePlate = dto.VehiclePlate;
            entity.ServiceArea = dto.ServiceArea;
            entity.ExpertiseAreas = dto.ExpertiseAreas;
            entity.CanViewFinance = dto.CanViewFinance;
            entity.CanViewAnalytics = dto.CanViewAnalytics;
            entity.CanDeleteRecords = dto.CanDeleteRecords;
            entity.CanApprovePurchase = dto.CanApprovePurchase;
            entity.CanAccessSettings = dto.CanAccessSettings;
        }

        // ======================== SERVICE JOB MAPPINGS ========================

        public static ServiceJobListItemDto ToListItemDto(this ServiceJob entity)
        {
            return new ServiceJobListItemDto
            {
                Id = entity.Id,
                Title = entity.Title,
                CustomerName = entity.Customer?.FullName ?? string.Empty,
                CustomerId = entity.CustomerId,
                Status = entity.Status,
                StatusDisplay = entity.StatusDisplay,
                Priority = entity.Priority,
                WorkOrderType = entity.WorkOrderType,
                WorkOrderTypeDisplay = entity.WorkOrderTypeDisplay,
                AssignedTechnician = entity.AssignedTechnician,
                ScheduledDate = entity.ScheduledDate,
                CreatedAt = entity.CreatedDate,
                SlaStatus = entity.SlaStatus,
                IsSlaBreached = entity.IsSlaBreached,
                TotalAmount = entity.TotalAmount
            };
        }

        public static ServiceJobDetailDto ToDetailDto(this ServiceJob entity)
        {
            return new ServiceJobDetailDto
            {
                Id = entity.Id,
                Title = entity.Title,
                Description = entity.Description,
                CustomerName = entity.Customer?.FullName ?? string.Empty,
                CustomerPhone = entity.Customer?.PhoneNumber ?? string.Empty,
                CustomerId = entity.CustomerId,
                Status = entity.Status,
                StatusDisplay = entity.StatusDisplay,
                Priority = entity.Priority,
                WorkOrderType = entity.WorkOrderType,
                WorkOrderTypeDisplay = entity.WorkOrderTypeDisplay,
                AssignedTechnician = entity.AssignedTechnician,
                AssignedUserId = entity.AssignedUserId,
                ScheduledDate = entity.ScheduledDate,
                CompletedDate = entity.CompletedDate,
                StartedAt = entity.StartedAt,
                CreatedAt = entity.CreatedDate,
                SlaDeadline = entity.SlaDeadline,
                SlaStatus = entity.SlaStatus,
                IsSlaBreached = entity.IsSlaBreached,
                EstimatedDuration = entity.EstimatedDuration,
                ActualDuration = entity.ActualDuration,
                DeviceBrand = entity.DeviceBrand,
                DeviceModel = entity.DeviceModel,
                SerialNumber = entity.SerialNumber,
                Accessories = entity.Accessories,
                PhysicalCondition = entity.PhysicalCondition,
                TechnicianNotes = entity.TechnicianNotes,
                Price = entity.Price,
                LaborCost = entity.LaborCost,
                DiscountAmount = entity.DiscountAmount,
                TaxAmount = entity.TaxAmount,
                TotalAmount = entity.TotalAmount,
                GpsLocation = entity.GpsLocation,
                IsOffSite = entity.IsOffSite,
                Source = entity.Source,
                IsCustomerApproved = entity.IsCustomerApproved,
                HasPhotos = entity.HasPhotos,
                BelongsToProject = entity.BelongsToProject
            };
        }
    }
}
