using Microsoft.AspNetCore.Identity;
using Sitiowebb.Data;
using Sitiowebb.Models;

namespace Sitiowebb;

public static class DataFixExtensions
{
    public static async Task FixUserDataAsync(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Fix any users missing CountryCode or TimeZoneId
            var users = context.Users.ToList();
            bool hasChanges = false;

            foreach (var user in users)
            {
                if (string.IsNullOrWhiteSpace(user.CountryCode))
                {
                    user.CountryCode = "ES";
                    hasChanges = true;
                }
                if (string.IsNullOrWhiteSpace(user.TimeZoneId))
                {
                    user.TimeZoneId = "Europe/Madrid";
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync();
                Console.WriteLine("✅ User data fixed successfully");
            }
        }
    }
}
