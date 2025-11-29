using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sitiowebb.Models;

namespace Sitiowebb.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<VacationRequest> VacationRequests => Set<VacationRequest>();
        public DbSet<Unavailability> Unavailabilities => Set<Unavailability>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // For PostgreSQL: convert INTEGER boolean columns to proper BOOLEAN type
            var boolToIntConverter = new ValueConverter<bool, int>(
                v => v ? 1 : 0,
                v => v == 1
            );

            // Apply conversion to all Identity boolean properties that were stored as int in old DB
            foreach (var entity in builder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    if (property.ClrType == typeof(bool) && 
                        (entity.Name.StartsWith("AspNetUsers") || entity.Name.StartsWith("IdentityUser")))
                    {
                        // These should be BOOLEAN in PostgreSQL, not INTEGER
                        // Don't apply the int converter for these
                    }
                }
            }

            // Only apply int converter to Unavailability.IsHalfDay
            builder.Entity<Unavailability>()
                .Property(u => u.IsHalfDay)
                .HasConversion(boolToIntConverter);

            // Configure manager-employee relationship
            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Manager)
                .WithMany(u => u.ManagedEmployees)
                .HasForeignKey(u => u.ManagerId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}