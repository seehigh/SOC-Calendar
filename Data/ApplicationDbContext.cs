using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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
    }
}