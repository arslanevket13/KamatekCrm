using KamatekCrm.Application.Common.Specifications;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Application.Features.Customers.Specifications
{
    public class CustomersByCitySpecification : BaseSpecification<Customer>
    {
        public CustomersByCitySpecification(string city) 
            : base(c => c.City != null && c.City.ToLower() == city.ToLower())
        {
            AddInclude(c => c.ServiceJobs);
            ApplyOrderBy(c => c.FullName);
        }
    }
}
