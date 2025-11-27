using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sitiowebb.Data;
using Sitiowebb.Models;

namespace Sitiowebb.Pages.ManagerOnly
{
    [Authorize(Roles = "Manager")]
    public class EmployesModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public EmployesModel(ApplicationDbContext db) => _db = db;

        // Fila de la tabla
        public record Row(
            string Email,
            string Name,
            string Status,
            string? Half,
            DateTime? From,
            DateTime? To,
            string CountryCode
        );

        public List<Row> Employes { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? q { get; set; }

        // --------- helpers de status ---------
        private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();

        private static string StatusFromUnav(string? kind, bool isHalf)
        {
            var k = Norm(kind);

            if (k is "vacation" or "vacations" or "holiday" or "holidays") return "vacation";
            if (k is "sick" or "ill" or "sickness")                         return "sick";
            if (k is "meeting" or "meet")                                   return "meeting";
            if (k is "trip" or "jobtrip" or "job trip")                     return "trip";
            if (k == "training")                                            return "training";
            if (k == "overtime")                                            return "overtime";
            if (k == "personal")                                            return "personal";
            if (isHalf)                                                     return "halfday";

            // 👉 Si es un texto libre (por ejemplo "doctor", "family", etc.),
            // lo devolvemos tal cual para que NO salga "unavailability".
            if (!string.IsNullOrWhiteSpace(kind))
                return kind.Trim();

            // Si no hay nada, consideramos que está disponible
            return "available";
        }

        // prioridad: qué gana sobre qué
        private static int StatusPriority(string status) => status switch
        {
            "sick"           => 7,
            "vacation"       => 6,
            "halfday"        => 5,
            "meeting"        => 4,
            "trip"           => 3,
            "training"       => 2,
            "overtime"       => 1,
            "personal"       => 1,
            "unavailability" => 0,   // por si tienes datos viejoNos con ese texto
            _                => -1   // available u otros
        };

        // =================== GET ===================
        public async Task OnGetAsync()
        {
            // Siempre usamos UTC para llevarnos bien con PostgreSQL
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            var needle = Norm(q);

            // 1) Usuarios
            var usersQ = _db.Users.AsNoTracking().Where(u => u.Email != null);

            if (!string.IsNullOrWhiteSpace(needle))
            {
                usersQ = usersQ.Where(u =>
                    (u.Email ?? "").ToLower().Contains(needle) ||
                    (u.UserName ?? "").ToLower().Contains(needle));
            }

            var users = await usersQ
                .Select(u => new
                {
                    EmailNorm   = (u.Email ?? "").Trim().ToLowerInvariant(),
                    Email       = u.Email ?? "",
                    Name        = string.IsNullOrWhiteSpace(u.UserName) ? (u.Email ?? "") : u.UserName!,
                    CountryCode = (u as ApplicationUser)!.CountryCode ?? ""   // "US", "CR", etc.
                })
                .OrderBy(u => u.Name)
                .ToListAsync();

            // 2) Unavailabilities ACTIVAS HOY (ni pasadas, ni solo futuras)
            //  Primero traemos TODO de la BD y después filtramos en memoria.
            //  Así evitamos que Postgres compare tipos raros (text vs timestamp).
            var unavsAll = await _db.Unavailabilities.AsNoTracking()
                .Select(u => new
                {
                    EmailNorm   = (u.UserEmail ?? "").Trim().ToLowerInvariant(),
                    u.Kind,
                    u.StartDate,
                    u.EndDate,
                    u.IsHalfDay,
                    u.HalfSegment
                })
                .ToListAsync();

            var unavs = unavsAll
                .Where(u => u.StartDate.Date <= today && u.EndDate.Date >= today)
                .ToList();

            var unavMap = unavs
                .Where(u => !string.IsNullOrWhiteSpace(u.EmailNorm))
                .GroupBy(u => u.EmailNorm)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 3) Construir filas
            var rows = new List<Row>();

            foreach (var u in users)
            {
                string status = "available";
                string? half  = null;
                DateTime? from = null;
                DateTime? to   = null;

                if (unavMap.TryGetValue(u.EmailNorm, out var list))
                {
                    string bestStatus = "available";
                    int bestPrio      = StatusPriority("available");
                    DateTime? bestFrom = null;
                    DateTime? bestTo   = null;
                    string? bestHalf   = null;

                    foreach (var r in list)
                    {
                        var s    = StatusFromUnav(r.Kind, r.IsHalfDay);
                        var prio = StatusPriority(s);

                        if (prio > bestPrio)
                        {
                            bestPrio  = prio;
                            bestStatus = s;
                            bestFrom   = r.StartDate.Date;
                            bestTo     = r.EndDate.Date;
                            bestHalf   = r.IsHalfDay ? (r.HalfSegment ?? "") : null;
                        }
                    }

                    status = bestStatus;
                    from   = bestFrom;
                    to     = bestTo;
                    half   = bestHalf;
                }

                rows.Add(new Row(
                    Email: u.Email,
                    Name: u.Name,
                    Status: status,
                    Half: half,
                    From: from,
                    To: to,
                    CountryCode: u.CountryCode
                ));
            }

            Employes = rows;
        }
    }
}