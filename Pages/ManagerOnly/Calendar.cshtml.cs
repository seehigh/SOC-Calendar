using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sitiowebb.Data;
using Sitiowebb.Models;
using Sitiowebb.Services;


namespace Sitiowebb.Pages.ManagerOnly
{
    [Authorize(Roles = "Manager")]
    public class CalendarModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public CalendarModel(ApplicationDbContext db) => _db = db;

        // -------- Parámetros de navegación / búsqueda --------
        [BindProperty(SupportsGet = true)] public int? Month { get; set; }
        [BindProperty(SupportsGet = true)] public int? Year  { get; set; }
        [BindProperty(SupportsGet = true)] public string? q { get; set; }

        // NUEVO: filtro por grupo de región (sin romper vistas antiguas)
        [BindProperty(SupportsGet = true)] public string? regionGroup { get; set; }
        public string? region => regionGroup; // alias para vistas que usaban Model.region

        // Helper de mapeo de grupos regionales -> códigos de país (por si lo usamos después)
        private static string[] CodesFor(string? group) => (group ?? "").ToUpperInvariant() switch
        {
            "NAM" => new[] { "US", "CA" }, // North America
            "CAM" => new[] { "MX", "CR", "GT", "SV", "HN", "NI", "PA" }, // Central America
            "SAM" => new[] { "AR", "BR", "CL", "CO", "PE", "UY", "PY", "BO", "EC", "VE" }, // South America
            "EU" => new[] { "ES", "FR", "DE", "IT", "PT", "UK", "NL" }, // Europe (ejemplo)
            "APAC" => new[] { "JP", "KR", "CN", "IN", "SG" }, // Asia-Pacific (ejemplo)
            "OCE" => new[] { "AU", "NZ" }, // Oceania
            _ => Array.Empty<string>()
        };
        private static readonly Dictionary<string, string[]> RegionGroups = new(StringComparer.OrdinalIgnoreCase)
{
    // Ajusta a tu gusto. Usamos los mismos grupos que en el dropdown.
    ["NAM"]  = new[] { "US", "MX" },           // North America
    ["CAM"]  = new[] { "CR" },                 // Central America
    ["SAM"]  = new[] { "AR", "BR" },           // South America
    ["EU"]   = new[] { "ES" },                 // Europe
    ["APAC"] = Array.Empty<string>(),          // (lo puedes poblar luego)
    ["OCE"]  = new[] { "AU", "NZ" }            // Oceania
};

private IEnumerable<string> ResolveHolidayCountries()
{
    // Si en la query pasas ?region=US se usa solo ese país
    var explicitCountry = (Request.Query["region"].ToString() ?? "").Trim().ToUpperInvariant();
    if (!string.IsNullOrWhiteSpace(explicitCountry))
        return new[] { explicitCountry };

    // Si hay grupo regional, unimos varios países
    var group = (regionGroup ?? "").Trim().ToUpperInvariant();
    if (!string.IsNullOrWhiteSpace(group) && RegionGroups.TryGetValue(group, out var codes) && codes.Length > 0)
        return codes;

    // Si no hay filtros, no mostramos feriados (puedes devolver US por defecto si quieres)
    return Array.Empty<string>();
}

        private void AddPublicHolidays(DateTime first, DateTime last)
        {
            var countries = ResolveHolidayCountries();
            if (!countries.Any()) return;

            foreach (var cc in countries)
            {
                foreach (var h in HolidayProvider.GetFor(cc, first.Year))
                {
                    if (h.Date < first || h.Date > last) continue;

                    var cell = Days.FirstOrDefault(c => c.Date.Date == h.Date.Date);
                    if (cell == null) continue;

                    // Mostramos el nombre del feriado + (PAÍS)
                    var text = $"{h.Name} ({cc})";

                 cell.Events.Add(new CalEvent(
                    h.Date,
                    text,
                    "holiday",
                    "k-holiday", // fuerza el estilo naranja
                    IconForKind("holiday")
                ));

                }
            }
        }

        // -------- Calendario --------
        // Color = clase CSS (k-vac, k-sick, k-meet, k-trip, k-half, k-train, k-default)
        public record CalEvent(DateTime Date, string User, string Kind, string Color, string Icon);
        public class DayCell
        {
            public DateTime Date { get; set; }
            public List<CalEvent> Events { get; set; } = new();
            public DayCell(DateTime d) { Date = d; }
        }

        public List<DayCell> Days { get; private set; } = new();
        public string MonthName { get; private set; } = "";
        public int MonthNum { get; private set; }
        public int YearNum  { get; private set; }

        // -------- Lista de empleados “hoy” (para la tabla debajo del calendario) --------
        public record EmpRow(string Email, string Name, string Status, string? Half, DateTime? From, DateTime? To);
        public List<EmpRow> Employes { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            // Mes/año a mostrar (en UTC)
            var now = DateTime.UtcNow;
            MonthNum = Month ?? now.Month;
            YearNum  = Year  ?? now.Year;

            // Fechas de inicio/fin del mes (marcadas como UTC para PostgreSQL)
            var firstLocal = new DateTime(YearNum, MonthNum, 1);
            var lastLocal  = firstLocal.AddMonths(1).AddDays(-1);

            var first = DateTime.SpecifyKind(firstLocal, DateTimeKind.Utc);
            var last  = DateTime.SpecifyKind(lastLocal,  DateTimeKind.Utc);

            MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(MonthNum);

            // Cuadrícula (42 celdas iniciando en lunes)
            var start = firstLocal;
            while (start.DayOfWeek != DayOfWeek.Monday) start = start.AddDays(-1);
            for (int i = 0; i < 42; i++) Days.Add(new DayCell(start.AddDays(i)));

            DayCell? Find(DateTime d) => Days.FirstOrDefault(c => c.Date.Date == d.Date);

            // ---------- Holidays personalizados (GetHolidays) ----------
            foreach (var h in GetHolidays(YearNum, regionGroup, region))
            {
                var cell = Find(h.date);
                if (cell == null) continue;

                cell.Events.Add(new CalEvent(
                    h.date,
                    h.name,
                    "holiday",
                    KindToCss("holiday"),
                    IconForKind("holiday")
                ));
            }

            // ---- FERIADOS PÚBLICOS SEGÚN PAÍS/REGIÓN SELECCIONADA ----
            AddPublicHolidays(firstLocal, lastLocal);

            // ---------- Otras indisponibilidades ---------- (sick/meeting/trip/halfday/training…)
            // 1) Traemos TODO lo necesario desde la BD
            var unavsAll = await _db.Unavailabilities.AsNoTracking()
                .Select(u => new { u.UserEmail, u.Kind, u.StartDate, u.EndDate })
                .ToListAsync();

            // 2) Filtramos en memoria por rango de fechas
            var unavsFiltered = unavsAll
                .Where(u => u.StartDate.Date <= last.Date && u.EndDate.Date >= first.Date);

            // 3) Filtro por texto (si hay search box)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var needle = q.Trim().ToUpperInvariant();
                unavsFiltered = unavsFiltered
                    .Where(u => (u.UserEmail ?? "").ToUpper().Contains(needle));
            }

            var unavs = unavsFiltered.ToList();

            foreach (var u in unavs)
            {
                var d0 = u.StartDate.Date;
                var d1 = u.EndDate.Date;
                for (var d = d0; d <= d1; d = d.AddDays(1))
                {
                    if (d < firstLocal || d > lastLocal) continue;
                    var cell = Find(d);
                    if (cell == null) continue;

                    var kind  = (u.Kind ?? "").Trim().ToLowerInvariant();
                    var css   = KindToCss(kind);
                    var icon  = IconForKind(kind);
                    var name  = PrettyFromEmail(u.UserEmail ?? "");

                    cell.Events.Add(new CalEvent(d, name, kind, css, icon));
                }
            }

            // Ordena chips por nombre para que se vea prolijo
            foreach (var c in Days) c.Events = c.Events.OrderBy(e => e.User).ToList();

            // =================== Tabla “Employees availability (today)” ===================
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var usersQ = _db.Users.AsNoTracking().Where(u => u.Email != null);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var needle = q.Trim().ToLowerInvariant();
                usersQ = usersQ.Where(u =>
                    (u.Email ?? "").ToLower().Contains(needle) ||
                    (u.UserName ?? "").ToLower().Contains(needle));
            }

            var users = await usersQ
                .Select(u => new { u.Email, u.UserName })
                .OrderBy(u => u.UserName)
                .ToListAsync();

            var vacAll = await _db.VacationRequests.AsNoTracking()
                .Where(v => v.Status == RequestStatus.Approved)
                .Select(v => new { v.UserEmail, v.From, v.To })
                .ToListAsync();

            var vacToday = vacAll
                .Where(v => v.From.Date <= today && v.To.Date >= today)
                .ToList();

            var vacMap = vacToday.GroupBy(v => v.UserEmail ?? "")
                                .ToDictionary(g => g.Key, g => g.First());

            // ---------- Indisponibilidades (unavailabilities) de HOY ----------
            var unavAllToday = await _db.Unavailabilities.AsNoTracking()
                .Select(u => new
                {
                    u.UserEmail,
                    u.Kind,
                    u.IsHalfDay,
                    u.HalfSegment,
                    u.StartDate,
                    u.EndDate
                })
                .ToListAsync();

            var unavToday = unavAllToday
                .Where(u => u.StartDate.Date <= today && u.EndDate.Date >= today)
                .ToList();

            var unavMap = unavToday
                .GroupBy(u => u.UserEmail ?? "")
                .ToDictionary(g => g.Key, g => g.ToList());


            foreach (var u in users)
            {
                var email = u.Email ?? "";
                var name  = string.IsNullOrWhiteSpace(u.UserName)
                    ? PrettyFromEmail(email)
                    : u.UserName!;

                string status      = "available";
                string? half       = null;
                DateTime? from     = null;
                DateTime? to       = null;
                int bestPriority   = StatusPriority(status);

                // 1) Vacaciones aprobadas hoy
                if (vacMap.TryGetValue(email, out var vrec))
                {
                    var s = "vacation";
                    status        = s;
                    bestPriority  = StatusPriority(s);
                    from          = vrec.From.Date;
                    to            = vrec.To.Date;
                }

                // 2) Otras indisponibilidades
                if (unavMap.TryGetValue(email, out var list))
                {
                    foreach (var r in list)
                    {
                        var s    = StatusFromUnav(r.Kind, r.IsHalfDay);
                        var prio = StatusPriority(s);

                        if (prio > bestPriority)
                        {
                            bestPriority = prio;
                            status       = s;
                            from         = r.StartDate.Date;
                            to           = r.EndDate.Date;
                            half         = r.IsHalfDay ? (r.HalfSegment ?? "") : null;
                        }
                    }
                }

                Employes.Add(new EmpRow(email, name, status, half, from, to));
            }

            Employes = Employes
                .OrderBy(r => r.Status == "available" ? 1 : 0) // no disponibles primero
                .ThenBy(r => r.Name)
                .ToList();

            return Page();
        }
        // ===================== Helpers visuales =====================
        private static string KindToCss(string? kind)
        {
            switch ((kind ?? "").Trim().ToLowerInvariant())
            {
                case "vacation":
                case "vacations":
                case "holiday":
                case "holidays":
                    return "k-holiday";  // <- Naranja claro

                case "hol":          // o si quieres separar:
                case "bankholiday":
                case "publicholiday":
                    return "k-hol";  // naranja (ver CSS abajo)

                case "sick":
                case "ill":
                case "sickness": return "k-sick";
                case "meeting":
                case "meet":     return "k-meet";
                case "trip":
                case "jobtrip":
                case "job trip": return "k-trip";
                case "training": return "k-train";
                case "halfday":
                case "half-day": return "k-half";
                default:         return "k-default";
            }
        }

                // ===================== Holidays por país / región =====================
        private static IEnumerable<(DateTime date, string name)> GetHolidays(
            int year,
            string? regionGroup,
            string? countryCode)
        {
            var list = new List<(DateTime, string)>();

            var region = (regionGroup ?? "").ToUpperInvariant();
            var country = (countryCode ?? "").ToUpperInvariant();

            bool isLatAm = region is "CAM" or "SAM"
                           || country is "MX" or "CR" or "AR" or "BR";
            bool isUS    = country == "US" || region == "NAM";
            bool isEU    = region == "EU" || country == "ES";

            // --- Festivos comunes (prácticamente todos) ---
            list.Add((new DateTime(year, 1, 1), "New year"));
            list.Add((new DateTime(year, 12, 25), "Christmas"));

            // --- LatAm / España: Nochebuena y Fin de año ---
            if (isLatAm || isEU)
            {
                list.Add((new DateTime(year, 12, 24), "Nochebuena"));
                list.Add((new DateTime(year, 12, 31), "New year’s eve"));
            }

            // --- Estados Unidos: Independence Day, Labor Day, Thanksgiving ---
            if (isUS)
            {
                list.Add((new DateTime(year, 7, 4), "Independence Day"));

                // Labor Day: primer lunes de septiembre
                list.Add((FirstMonday(year, 9), "Labor Day"));

                // Thanksgiving: cuarto jueves de noviembre
                list.Add((NthWeekdayOfMonth(year, 11, DayOfWeek.Thursday, 4), "Thanksgiving"));
            }

            // --- Ejemplo: España ---
            if (country == "ES")
            {
                list.Add((new DateTime(year, 1, 6), "Epiphany"));
            }

            // Aquí puedes seguir añadiendo más días específicos
            // para cada país o región si quieres.

            return list;
        }

        private static DateTime FirstMonday(int year, int month)
        {
            var d = new DateTime(year, month, 1);
            while (d.DayOfWeek != DayOfWeek.Monday)
                d = d.AddDays(1);
            return d;
        }

        private static DateTime NthWeekdayOfMonth(int year, int month, DayOfWeek dow, int n)
        {
            var d = new DateTime(year, month, 1);
            while (d.DayOfWeek != dow)
                d = d.AddDays(1);
            return d.AddDays(7 * (n - 1));
        }


        private static string IconForKind(string? kind)
        {
            var k = (kind ?? "").Trim().ToLowerInvariant();
            return k switch
            {
                "holiday" or "holidays" or "vacation" or "vacations" => "/images/holiday.png",
                "sick" or "ill" or "sickness"                        => "/images/bed.png",
                "meeting" or "meet"                                  => "/images/headset.png",
                "trip" or "jobtrip" or "job trip"                    => "/images/plane.png",
                "halfday" or "half-day"                              => "/images/user-round-minus.png",
                "training"                                           => "/images/user-round-minus.png",
                _                                                    => "/images/user-round-minus.png"
            };
        }

        

        // ===================== Helpers de estado “hoy” =====================
        private static string Normalize(string? k) => (k ?? "").Trim().ToLowerInvariant();

        private static string StatusFromUnav(string? kind, bool isHalf)
        {
            var k = Normalize(kind);

            if (k is "vacation" or "vacations" or "holiday" or "holidays") return "vacation";
            if (k is "sick" or "ill" or "sickness") return "sick";
            if (k is "meeting" or "meet") return "meeting";
            if (k is "trip" or "jobtrip" or "job trip") return "trip";
            if (k == "training") return "training";
            if (k == "overtime") return "overtime";
            if (k == "personal") return "personal";
            if (isHalf) return "halfday";
            return "unavailability";
        }
        // Prioridad para decidir qué estado gana
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
            "unavailability" => 0,
            _                => -1   // available
        };

        private static string PrettyFromEmail(string email)
        {
            try
            {
                var local = (email ?? "").Split('@')[0]
                    .Replace('.', ' ')
                    .Replace('_', ' ')
                    .Replace('-', ' ')
                    .Trim();
                return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(local);
            }
            catch { return string.IsNullOrWhiteSpace(email) ? "(Unknown)" : email; }
        }
    }
}
