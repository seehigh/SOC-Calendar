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

            // ===== NUEVO: País =====
            [Required]
            [Display(Name = "Country (ISO-2)")]
            [RegularExpression(@"^[A-Za-z]{2}$", ErrorMessage = "Usa un código ISO-2 válido (p. ej., US, ES, MX, CR, AR, BR, AU).")]
            public string CountryCode { get; set; } = "US";   // valor por defecto

            // ===== NUEVO: Manager Assignment =====
            [Display(Name = "Manager")]
            public string? ManagerId { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            // Set default country
            if (string.IsNullOrWhiteSpace(Input.CountryCode))
                Input.CountryCode = "US";

            // Load available managers (users with "Manager" role)
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

            // Normalize country code
            var cc = (Input.CountryCode ?? "").Trim().ToUpperInvariant();

            if (cc.Length != 2)
                ModelState.AddModelError(nameof(Input.CountryCode), "Código de país inválido.");

            if (!ModelState.IsValid)
                return Page();

            var user = new ApplicationUser
            {
                UserName       = Input.UserName,
                Email          = Input.Email,
                EmailConfirmed = true,
                CountryCode    = cc,         // <<< save country
                TimeZoneId     = "Etc/UTC",  // <<< default timezone
                ManagerId      = Input.ManagerId  // <<< assign manager
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

            // Redirect to user home
            return LocalRedirect(Url.Content("~/UsuarioHome"));
        }
    }
}
