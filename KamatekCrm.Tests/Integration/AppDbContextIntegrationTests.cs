using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        [Fact]
        public void DbContextFactory_CreateDbContext_ResolvesSuccessfully_WithoutUserContext()
        {
            var services = new ServiceCollection();
            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_1"));
            var sp = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var context = factory.CreateDbContext();
            context.Should().NotBeNull();
        }

        [Fact]
        public void DbContextFactory_CreateDbContext_ResolvesSuccessfully_WithUserContext()
        {
            var services = new ServiceCollection();
            services.AddSingleton<KamatekCrm.ApplicationCore.Interfaces.ICurrentUserContext>(new TestUserContext());
            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_2"));
            var sp = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var context = factory.CreateDbContext();
            context.Should().NotBeNull();
            context.CurrentUserContext.Should().NotBeNull();
        }

        [Fact]
        public void ServiceProvider_ResolvesTransientAppDbContext_AndUnitOfWork()
        {
            var services = new ServiceCollection();
            services.AddSingleton<KamatekCrm.ApplicationCore.Interfaces.ICurrentUserContext>(new TestUserContext());
            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_3"));
            services.AddTransient<AppDbContext>(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
            services.AddScoped<KamatekCrm.Shared.Repositories.IUnitOfWork, KamatekCrm.Infrastructure.Repositories.UnitOfWork>();

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Should().NotBeNull();
            dbContext.CurrentUserContext.Should().NotBeNull();

            var uow = scope.ServiceProvider.GetRequiredService<KamatekCrm.Shared.Repositories.IUnitOfWork>();
            uow.Should().NotBeNull();
            uow.Repository<Customer>().Should().NotBeNull();
        }

        private sealed class TestUserContext : KamatekCrm.ApplicationCore.Interfaces.ICurrentUserContext
        {
            public bool IsAuthenticated => true;
            public int? UserId => 1;
            public string Username => "test";
            public string Role => "Admin";
            public bool HasPermission(KamatekCrm.ApplicationCore.Security.ApplicationPermission permission) => true;
        }
    }
}

