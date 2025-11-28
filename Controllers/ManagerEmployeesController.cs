using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sitiowebb.Data;

namespace Sitiowebb.Controllers
{
    [Authorize(Roles = "Manager")]
    [Route("api/manager")]
    [ApiController]
    public class ManagerEmployesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public ManagerEmployesController(ApplicationDbContext db) => _db = db;
            private static string Normalize(string? k)
            {
                return (k ?? string.Empty).Trim().ToLowerInvariant();
            }


        public class EmployeStatusDto
        {
            public string Email { get; set; } = "";
            public string Name  { get; set; } = "";
            public string Status { get; set; } = "";   // available | sick | vacation | trip | meeting | halfday | personal
            public string? Half { get; set; }          // AM | PM (cuando aplique)
            public DateTime? From { get; set; }
            public DateTime? To   { get; set; }
        }

        // GET /api/manager/employees/status?q=texto
        [HttpGet("employes/status")]
        public async Task<IActionResult> GetStatuses([FromQuery] string? q)
        {
            var today = DateTime.Today;

            // Usuarios (Nombre/Email)
            var usersQuery = _db.Users
                .Where(u => u.Email != null)
                .Select(u => new { u.Email, u.UserName });

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qq = q.Trim().ToLower();
                usersQuery = usersQuery.Where(u =>
                    (u.Email ?? "").ToLower().Contains(qq) ||
                    (u.UserName ?? "").ToLower().Contains(qq));
            }

            var users = await usersQuery.ToListAsync();

            // Ausencias activas hoy por email
            var unavToday = await _db.Unavailabilities
                .Where(u => u.StartDate.Date <= today && u.EndDate.Date >= today)
                .ToListAsync();

            // Prioridad de estado para mostrar si hay varias
            int Priority(string? kind) => (kind?.ToLower()) switch
            {
                "sick" or "ill"     => 0,
                "vacation"          => 1,
                "trip" or "jobtrip" => 2,
                "meeting"           => 3,
                "halfday"           => 4,
                "personal"          => 5,
                _                   => 9
            };

            string Normalize(string? k) => (k ?? "").Trim().ToLower() switch
            {
                "ill"      => "sick",
                "jobtrip"  => "trip",
                "half day" => "halfday",
                _ => (k ?? "").Trim().ToLower()
            };

            var list = users.Select(u =>
            {
                var items = unavToday
                    .Where(a => string.Equals(a.UserEmail, u.Email, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(a => Priority(a.Kind))
                    .ToList();

                if (items.Count == 0)
                {
                    return new EmployeStatusDto
                    {
                        Email  = u.Email ?? "",
                        Name   = u.UserName ?? u.Email ?? "",
                        Status = "available"
                    };
                }

                var top = items.First();
                return new EmployeStatusDto
                {
                    Email  = u.Email ?? "",
                    Name   = u.UserName ?? u.Email ?? "",
                    Status = Normalize(top.Kind),
                    Half   = top.IsHalfDay ? (top.HalfSegment ?? "") : null,
                    From   = top.StartDate,
                    To     = top.EndDate
                };
            })
            // orden: primero no disponibles (por prioridad), luego disponibles
            .OrderBy(e => e.Status == "available" ? 1 : 0)
            .ThenBy(e => e.Name)
            .ToList();

            return Ok(list);
        }
    }
}
