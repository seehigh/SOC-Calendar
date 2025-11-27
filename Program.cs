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
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

// --- DB (PostgreSQL) ---
var connectionString =
    builder.Environment.IsDevelopment()
        ? builder.Configuration.GetConnectionString("DefaultConnection")   // local
        : Environment.GetEnvironmentVariable("DATABASE_URL");              // Railway

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));


builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

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
    options.Conventions.AuthorizePage("/ManagerOnly/Index");
});

// -------- Email (SMTP / MailerSend) -------------
builder.Services.Configure<EmailSettings>(options =>
{
    // 1) Carga base desde appsettings.*.json
    builder.Configuration.GetSection("EmailSettings").Bind(options);

    // 2) Sobrescribe con variables de entorno de Railway (si existen)
    void Override(string envName, Action<string> assign)
    {
        var value = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            assign(value);
        }
    }

    Override("EMAILSETTINGS__FROM",      v => options.From = v);
    Override("EMAILSETTINGS__FROMNAME",  v => options.FromName = v);
    Override("EMAILSETTINGS__HOST",      v => options.Host = v);
    Override("EMAILSETTINGS__USERNAME",  v => options.UserName = v);
    Override("EMAILSETTINGS__PASSWORD",  v => options.Password = v);

    var portStr = Environment.GetEnvironmentVariable("EMAILSETTINGS__PORT");
    if (int.TryParse(portStr, out var port))
    {
        options.Port = port;
    }

    var sslStr = Environment.GetEnvironmentVariable("EMAILSETTINGS__ENABLESSL");
    if (bool.TryParse(sslStr, out var ssl))
    {
        options.EnableSsl = ssl;
    }

    // Si algún día volvemos a usar ApiKey (por API)
    var apiKey = Environment.GetEnvironmentVariable("EMAILSETTINGS__APIKEY");
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        options.ApiKey = apiKey;
    }
});

// Registramos el sender que usa SmtpClient
builder.Services.AddTransient<IAppEmailSender, SmtpAppEmailSender>();

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
    try
    {
        var db     = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var seeder = services.GetRequiredService<IdentitySeeder>(); // si tienes este seeder
        await seeder.SeedAsync();                                   // crea roles y managers si faltan
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Seeding error: {ex.Message}");
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
