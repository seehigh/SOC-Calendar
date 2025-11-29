using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Sitiowebb.Models; // ApplicationUser

namespace Sitiowebb.Data
{
    public class IdentitySeeder
    {
        // Nombres de rol
        public const string RoleUsuario = "Usuario";
        public const string RoleManager = "Manager";

        private readonly RoleManager<IdentityRole> _roles;
        private readonly UserManager<ApplicationUser> _users;
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _dbContext;

        public IdentitySeeder(
            RoleManager<IdentityRole> roles,
            UserManager<ApplicationUser> users,
            IConfiguration config,
            ApplicationDbContext dbContext)
        {
            _roles  = roles;
            _users  = users;
            _config = config;
            _dbContext = dbContext;
        }

        public async Task SeedAsync()
        {
            // 1) Asegurar roles
            if (!await _roles.RoleExistsAsync(RoleUsuario))
                await _roles.CreateAsync(new IdentityRole(RoleUsuario));

            if (!await _roles.RoleExistsAsync(RoleManager))
                await _roles.CreateAsync(new IdentityRole(RoleManager));

            // 2) Managers desde configuración (opcional)
            var managersFromConfig =
                _config.GetSection("Seed:Managers").Get<List<Dictionary<string, string>>>();

            if (managersFromConfig != null)
            {
                foreach (var m in managersFromConfig)
                {
                    var email = m.TryGetValue("Email", out var e) ? e : null;
                    var pwd   = m.TryGetValue("Password", out var p) ? p : null;
                    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pwd))
                        continue;

                    var existing = await _users.FindByEmailAsync(email);
                    if (existing == null)
                    {
                        var newUser = new ApplicationUser
                        {
                            UserName       = email,
                            Email          = email,
                            EmailConfirmed = true,
                            CountryCode    = "ES",  // Default for seeded managers
                            TimeZoneId     = "Europe/Madrid"  // Default timezone
                        };

                        var createRes = await _users.CreateAsync(newUser, pwd);
                        if (createRes.Succeeded)
                            await _users.AddToRoleAsync(newUser, RoleManager);
                    }
                    else if (!await _users.IsInRoleAsync(existing, RoleManager))
                    {
                        await _users.AddToRoleAsync(existing, RoleManager);
                    }
                }
            }

            // 3) Assign managers to employees (non-manager users without a manager)
            var allUsers = await _dbContext.Users.ToListAsync();
            var managers = new List<ApplicationUser>();
            
            // Filter managers
            foreach (var user in allUsers)
            {
                if (await _users.IsInRoleAsync(user, RoleManager))
                {
                    managers.Add(user);
                }
            }

            if (managers.Count > 0)
            {
                var employeesWithoutManager = allUsers
                    .Where(u => !managers.Contains(u) && u.ManagerId == null)
                    .ToList();

                // Round-robin assignment: distribute employees among managers
                for (int i = 0; i < employeesWithoutManager.Count; i++)
                {
                    var managerIndex = i % managers.Count;
                    employeesWithoutManager[i].ManagerId = managers[managerIndex].Id;
                }

                await _dbContext.SaveChangesAsync();
            }

            // 4) Promover "a mano" un correo existente (opcional)
            var emailManual = _config["Seed:PromoteEmail"]; // p.ej. "sararomerosuarez@gmail.com"
            if (!string.IsNullOrWhiteSpace(emailManual))
            {
                var user = await _users.FindByEmailAsync(emailManual);
                if (user != null && !await _users.IsInRoleAsync(user, RoleManager))
                    await _users.AddToRoleAsync(user, RoleManager);
            }
        }
    }
}
