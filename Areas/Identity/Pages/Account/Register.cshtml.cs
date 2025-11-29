using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;   // [AllowAnonymous]
using Sitiowebb.Data;
using Sitiowebb.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace Sitiowebb.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly ApplicationDbContext _dbContext;

        public RegisterModel(UserManager<ApplicationUser> userManager,
                             SignInManager<ApplicationUser> signInManager,
                             ILogger<RegisterModel> logger,
                             ApplicationDbContext dbContext)
        {
            _userManager   = userManager;
            _signInManager = signInManager;
            _logger        = logger;
            _dbContext     = dbContext;
        }

        // Managers available for selection
        public List<ApplicationUser> AvailableManagers { get; set; } = new();

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            [Display(Name = "User name")]
            [StringLength(32, MinimumLength = 3, ErrorMessage = "El usuario debe tener entre 3 y 32 caracteres.")]
            public string UserName { get; set; } = string.Empty;

            [Required, EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required, StringLength(100, MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            // ===== NUEVO: País y Zona horaria =====
            [Required]
            [Display(Name = "Country (ISO-2)")]
            [RegularExpression(@"^[A-Za-z]{2}$", ErrorMessage = "Usa un código ISO-2 válido (p. ej., US, ES, MX, CR, AR, BR, AU).")]
            public string CountryCode { get; set; } = "US";   // valor por defecto

            [Required]
            [Display(Name = "Time zone (IANA)")]
            public string TimeZoneId { get; set; } = "Etc/UTC"; // valor por defecto

            // ===== NUEVO: Manager Assignment =====
            [Display(Name = "Manager")]
            public string? ManagerId { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            // Puedes dar defaults amigables aquí si quieres
            if (string.IsNullOrWhiteSpace(Input.CountryCode))
                Input.CountryCode = "US";
            if (string.IsNullOrWhiteSpace(Input.TimeZoneId))
                Input.TimeZoneId = "Etc/UTC";

            // Load available managers: get all users, then filter those with Manager role
            var allUsers = _dbContext.Users.OrderBy(u => u.Email).ToList();
            AvailableManagers = new List<ApplicationUser>();
            
            foreach (var user in allUsers)
            {
                if (await _userManager.IsInRoleAsync(user, "Manager"))
                {
                    AvailableManagers.Add(user);
                }
            }
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return Page();

            // Normaliza país (ISO-2) y TZ
            var cc = (Input.CountryCode ?? "").Trim().ToUpperInvariant();
            var tz = (Input.TimeZoneId  ?? "").Trim();

            if (cc.Length != 2)
                ModelState.AddModelError(nameof(Input.CountryCode), "Código de país inválido.");
            if (string.IsNullOrWhiteSpace(tz))
                ModelState.AddModelError(nameof(Input.TimeZoneId), "Zona horaria requerida.");

            if (!ModelState.IsValid)
                return Page();

            var user = new ApplicationUser
            {
                UserName       = Input.UserName,
                Email          = Input.Email,
                EmailConfirmed = true,
                CountryCode    = cc,         // <<< guarda país
                TimeZoneId     = tz,         // <<< guarda zona horaria
                ManagerId      = Input.ManagerId  // <<< asigna manager
            };

            var result = await _userManager.CreateAsync(user, Input.Password);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);
                return Page();
            }

            _logger.LogInformation("Usuario creado exitosamente.");
            await _signInManager.SignInAsync(user, isPersistent: false);

            // Redirige SIEMPRE al home de usuario
            return LocalRedirect(Url.Content("~/UsuarioHome"));
            // Si prefieres respetar ReturnUrl cuando sea local:
            // return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Content("~/UsuarioHome"));
        }
    }
}
