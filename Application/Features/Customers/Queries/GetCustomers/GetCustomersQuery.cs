using System.Collections.Generic;
using MediatR;
using KamatekCrm.Application.Common.Interfaces;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Application.Features.Customers.Queries.GetCustomers
{
    public class GetCustomersQuery : IQuery<List<Customer>>
    {
    }
}
