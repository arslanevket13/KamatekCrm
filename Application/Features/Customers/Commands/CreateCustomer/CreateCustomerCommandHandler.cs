using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using KamatekCrm.Application.Common.Models;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Application.Features.Customers.Commands.CreateCustomer
{
    public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Result>
    {
        private readonly AppDbContext _context;

        public CreateCustomerCommandHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Result> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = new Customer
                {
                    CustomerCode = request.CustomerCode,
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
                    Email = request.Email,
                    City = request.City,
                    District = request.District,
                    Neighborhood = request.Neighborhood,
                    Street = request.Street,
                    BuildingNo = request.BuildingNo,
                    ApartmentNo = request.ApartmentNo,
                    Notes = request.Notes,
                    Type = request.Type,
                    TcKimlikNo = request.TcKimlikNo,
                    CompanyName = request.CompanyName,
                    TaxNumber = request.TaxNumber,
                    TaxOffice = request.TaxOffice,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude
                };

                _context.Customers.Add(entity);

                await _context.SaveChangesAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex.Message);
            }
        }
    }
}
