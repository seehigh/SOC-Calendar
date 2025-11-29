// Sitiowebb/Models/ApplicationUser.cs
using System;
using Microsoft.AspNetCore.Identity;

namespace Sitiowebb.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? Country { get; set; }
        // ISO 3166-1 alpha-2 ("US", "ES", "MX", "FR", etc)
        public string CountryCode { get; set; } = "ES";

        // IANA time zone id ("Europe/Madrid", "America/New_York", etc)
        public string TimeZoneId { get; set; } = "Europe/Madrid";

        // Manager assignment
        public string? ManagerId { get; set; }
        public ApplicationUser? Manager { get; set; }
        
        // Employees under this manager
        public ICollection<ApplicationUser> ManagedEmployees { get; set; } = new List<ApplicationUser>();
    }
}
