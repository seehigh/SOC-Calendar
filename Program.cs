// -------------------- USINGS --------------------
using Sitiowebb.Models; // <-- NECESARIO para RequestStatus
using Sitiowebb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Sitiowebb.Data;        // ApplicationDbContext
using Sitiowebb;             // ApplicationUser (si lo tienes en este namespace)
using Sitiowebb.Data.Hubs;
using Microsoft.AspNetCore.SignalR;
 
// -------------------- BUILDER --------------------
var builder = WebApplication.CreateBuilder(args);

// PostgreSQL support for production
if (!builder.Environment.IsDevelopment())
{
    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
}

// --- DB Configuration ---
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("No se encontró la cadena de conexión");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.UseSqlite(connectionString);  // SQLite for local development
    }
    else
    {
        options.UseNpgsql(connectionString);  // PostgreSQL for production
    }
});


builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);

// Only load environment variables in production
if (!builder.Environment.IsDevelopment())
{
    builder.Configuration.AddEnvironmentVariables();
}

// ---------------- Identity + Roles ----------------
builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 5;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// ---------------- Cookies (login/denegado) ----------------
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Identity/Account/Login";
    o.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// ---------------- SignalR + CORS ----------------
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, EmailUserIdProvider>();
builder.Services.AddCors(o =>
{
    o.AddPolicy("SignalR", p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .SetIsOriginAllowed(_ => true));
});

// ---------------- Razor Pages + Convenciones ----------------
builder.Services.AddRazorPages(options =>
{
    // públicas
    options.Conventions.AllowAnonymousToPage("/Index");
    options.Conventions.AllowAnonymousToPage("/Privacy");
    options.Conventions.AllowAnonymousToPage("/Error");

    // privadas (requieren login)
    options.Conventions.AuthorizePage("/UsuarioHome");
    options.Conventions.AuthorizePage("/Available");
    options.Conventions.AuthorizePage("/Unavailable");
    options.Conventions.AuthorizePage("/UnavailableOptions");
    options.Conventions.AuthorizePage("/VacationRequest");

    // menú solo para managers (área ManagerOnly)
    options.Conventions.AuthorizeFolder("/ManagerOnly", "ManagersOnly");
});

// -------- Email (MailerSend API) -------------
builder.Services.Configure<EmailSettings>(options =>
{
    // Lee "EmailSettings" de appsettings.* (útil en local)
    builder.Configuration.GetSection("EmailSettings").Bind(options);

    // Sobrescribe con variables de entorno en Railway (producción)
    var from = Environment.GetEnvironmentVariable("EMAILSETTINGS__FROM");
    if (!string.IsNullOrWhiteSpace(from))
        options.From = from;

    var fromName = Environment.GetEnvironmentVariable("EMAILSETTINGS__FROMNAME");
    if (!string.IsNullOrWhiteSpace(fromName))
        options.FromName = fromName;

    var apiKey = Environment.GetEnvironmentVariable("EMAILSETTINGS__APIKEY");
    if (!string.IsNullOrWhiteSpace(apiKey))
        options.ApiKey = apiKey;
});

// HttpClient para MailerSend
builder.Services.AddHttpClient<MailerEmailSender>();

// Usar MailerEmailSender en toda la app donde se pide IAppEmailSender
builder.Services.AddTransient<IAppEmailSender, MailerEmailSender>();

// ---------------- Autorización por rol ----------------
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("ManagersOnly", p => p.RequireRole("Manager"));
});

builder.Services.AddControllers();

// 🔹 REGISTRA EL SEEDER AQUÍ (una sola vez, sin caracteres extra)
builder.Services.AddScoped<IdentitySeeder>();

// -------------------- APP -------------------------
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// -------------------- Pipeline --------------------
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("SignalR");        // <- CORS debe ir después de UseRouting

app.UseAuthentication();       // <- SIEMPRE antes de Authorization
app.UseAuthorization();

// -------------------- Endpoints -------------------
app.MapRazorPages();
app.MapControllers();

//---------------------prueba,eliminar luego-----------------------------------
app.MapGet("/test-email", async (IAppEmailSender email) =>
{
    await email.SendAsync("saramorerasuarez@gmail.com", "Test Email", "<b>Funciona!</b>");
    return "Email enviado!";
});

// Hubs de SignalR
app.MapHub<Sitiowebb.Data.Hubs.NotificationsHub>("/hubs/notifications");
// Redirección raíz a /Index (opcional)
app.MapGet("/", ctx =>
{
    ctx.Response.Redirect("/Index");
    return Task.CompletedTask;
});

// -------- Seeding de roles/usuarios manager (opcional) --------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        
        logger.LogInformation("Attempting to apply migrations...");
        await db.Database.MigrateAsync();
        logger.LogInformation("✅ Migrations applied successfully");

        var seeder = services.GetRequiredService<IdentitySeeder>();
        await seeder.SeedAsync();
        logger.LogInformation("✅ Seeding completed successfully");

        // Fix any users with missing CountryCode or TimeZoneId
        await app.FixUserDataAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Database initialization error - app will continue without migrations");
        // Don't rethrow - let the app start even if DB connection fails
    }
}

// --------- Helper DEV: ascender un usuario a Manager ---------
app.MapGet("/dev/make-manager/{email}", async (
    string email,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles) =>
{
    var user = await users.FindByEmailAsync(email);
    if (user is null) return Results.NotFound($"User '{email}' not found.");

    if (!await roles.RoleExistsAsync("Manager"))
        await roles.CreateAsync(new IdentityRole("Manager"));

    if (!await users.IsInRoleAsync(user, "Manager"))
        await users.AddToRoleAsync(user, "Manager");

    return Results.Ok($"{email} is now Manager.");
});

app.Run();


// ===================================================================
//  Servicio mínimo de email (No-Op) para compilar sin proveedor real
//  (Si ya lo moviste a Services/NullEmailSender.cs, elimina esta clase)
// ===================================================================
public class NullEmailSender : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
        => Task.CompletedTask;

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
        => Task.CompletedTask;

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
        => Task.CompletedTask;

    public Task SendEmailAsync(ApplicationUser user, string email, string subject, string htmlMessage)
        => Task.CompletedTask;
}