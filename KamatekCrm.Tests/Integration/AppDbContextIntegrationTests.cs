using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KamatekCrm.Tests.Integration
{
    public class AppDbContextIntegrationTests
    {
        private static AppDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public async Task InsertAndRetrieve_ServiceJob_ShouldSaveAndRetrieveFlawlessly()
        {
            // Arrange
            using var context = CreateInMemoryDbContext();

            var customer = new Customer
            {
                FullName = "Ahmet Yılmaz",
                CustomerCode = "CUST-001",
                PhoneNumber = "0555 123 4567",
                Type = CustomerType.Individual
            };

            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var serviceJob = new ServiceJob
            {
                CustomerId = customer.Id,
                Description = "Kamera Montajı ve Kablolama",
                JobCategory = JobCategory.CCTV,
                WorkOrderType = WorkOrderType.Installation,
                Status = JobStatus.Pending,
                Priority = JobPriority.High,
                CreatedDate = DateTime.UtcNow,
                Price = 2500.00m
            };

            // Act
            context.ServiceJobs.Add(serviceJob);
            await context.SaveChangesAsync();

            // Assert
            using var queryContext = CreateInMemoryDbContext();
            // Verify in same database instance
            var savedJob = await context.ServiceJobs
                .Include(j => j.Customer)
                .FirstOrDefaultAsync(j => j.Id == serviceJob.Id);

            savedJob.Should().NotBeNull();
            savedJob!.Description.Should().Be("Kamera Montajı ve Kablolama");
            savedJob.Status.Should().Be(JobStatus.Pending);
            savedJob.Price.Should().Be(2500.00m);
            savedJob.Customer.Should().NotBeNull();
            savedJob.Customer!.FullName.Should().Be("Ahmet Yılmaz");
        }
    }
}
