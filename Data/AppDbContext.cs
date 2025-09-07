using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Customer> Customers { get; set; }

        public DbSet<Ticket> Tickets { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite("Data Source=kamatekcrm.db");
        }
    }
}
