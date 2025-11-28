using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sitiowebb.Data;
using Sitiowebb.Data.Hubs;
using Sitiowebb.Models;
using Sitiowebb.Services;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;

namespace Sitiowebb.Pages
{
    [Authorize]
    [ValidateAntiForgeryToken]
    public class VacationRequestModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<NotificationsHub> _hub;
        private readonly IAppEmailSender _emailSender;
        private readonly UserManager<ApplicationUser> _userManager;

        public VacationRequestModel(
            ApplicationDbContext db, 
            IHubContext<NotificationsHub> hub,
            IAppEmailSender emailSender,
            UserManager<ApplicationUser> userManager)
        {
            _db  = db;
            _hub = hub;
            _emailSender = emailSender;
            _userManager = userManager;
        }

        [BindProperty, Required]
        public string From { get; set; } = "";

        [BindProperty, Required]
        public string To { get; set; } = "";

        public async Task<IActionResult> OnPostAsync()
        {
            // 1) Validar fechas
            var formats = new[] { "dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yyyy", "yyyy-MM-dd" };
            if (!DateTime.TryParseExact(From, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate))
            {
                ModelState.AddModelError(nameof(From), "Invalid date. Use dd/MM/yyyy.");
                return Page();
            }
            if (!DateTime.TryParseExact(To, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
            {
                ModelState.AddModelError(nameof(To), "Invalid date. Use dd/MM/yyyy.");
                return Page();
            }
            if (fromDate > toDate)
            {
                ModelState.AddModelError(string.Empty, "The start date must be before the end date.");
                return Page();
            }

            // 2) Usuario actual
            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "";

            // 3) Crear solicitud - convert DateTime to DateTimeOffset
            var req = new VacationRequest
            {
                From       = new DateTimeOffset(fromDate, TimeSpan.Zero),
                To         = new DateTimeOffset(toDate, TimeSpan.Zero),
                CreatedUtc = DateTimeOffset.UtcNow,
                Status     = RequestStatus.Pending,
                UserEmail  = userEmail,
                Kind       = "vacation"
            };

            _db.VacationRequests.Add(req);
            await _db.SaveChangesAsync();

            // 4) Notificaciones a MANAGERS
            // tras SaveChangesAsync():
            var pending = await _db.VacationRequests.CountAsync(v => v.Status == RequestStatus.Pending);

            // badge managers
            await _hub.Clients.Group("managers").SendAsync("pendingCountUpdated", new { count = pending });

            // tarjetita managers
            await _hub.Clients.Group("managers").SendAsync("vacationRequestCreated", new {
                id   = req.Id,
                user = (req.UserEmail ?? string.Empty).ToLowerInvariant(),
                from = req.From,
                to   = req.To
            });

            // 5) Enviar emails a todos los managers
            var managers = await _userManager.GetUsersInRoleAsync("Manager");
            foreach (var manager in managers)
            {
                if (string.IsNullOrWhiteSpace(manager.Email))
                    continue;

                var fromDateStr = req.From.ToString("dd/MM/yyyy");
                var toDateStr = req.To.ToString("dd/MM/yyyy");
                var emailSubject = $"New vacation request from {userEmail}";
                var emailBody = EmailTemplate.Build(
                    title: "New Vacation Request",
                    introText: $"Hi {manager.UserName}, you have a new vacation request to review:",
                    mainText: $"{userEmail} has requested vacation from <strong>{fromDateStr}</strong> to <strong>{toDateStr}</strong>.",
                    buttonText: "Review Request",
                    buttonUrl: $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/ManagerOnly/Requests"
                );

                await _emailSender.SendAsync(manager.Email, emailSubject, emailBody);
            }

            // 6) Mensaje UI y redirect
            TempData["SuccessMessage"] = "Request sent.";
            return RedirectToPage("/UsuarioHome");
        }
    }
}
