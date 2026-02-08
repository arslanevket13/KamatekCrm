using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Application.Features.Customers.Queries.GetCustomers
{
    public class GetCustomersQueryHandler : IRequestHandler<GetCustomersQuery, List<Customer>>
    {
        private readonly AppDbContext _context;

        public GetCustomersQueryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Customer>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
        {
            return await _context.Customers
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }
    }
}
