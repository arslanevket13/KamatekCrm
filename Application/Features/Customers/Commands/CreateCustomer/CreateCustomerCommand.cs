using MediatR;
using KamatekCrm.Application.Common.Interfaces;
using KamatekCrm.Application.Common.Models;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Application.Features.Customers.Commands.CreateCustomer
{
    public class CreateCustomerCommand : ICommand<Result>
    {
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
        public CustomerType Type { get; set; } = CustomerType.Individual;
        public string? TcKimlikNo { get; set; }
        public string? CompanyName { get; set; }
        public string? TaxNumber { get; set; }
        public string? TaxOffice { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
