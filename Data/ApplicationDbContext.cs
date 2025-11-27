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

            // Conversor bool <-> int SOLO para Unavailability.IsHalfDay
            var boolToIntConverter = new ValueConverter<bool, int>(
                v => v ? 1 : 0,
                v => v == 1
            );

            builder.Entity<Unavailability>()
                .Property(u => u.IsHalfDay)
                .HasConversion(boolToIntConverter);
        }
    }
}