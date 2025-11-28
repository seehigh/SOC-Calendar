# Sitiowebb Copilot Instructions

## Project Overview
**Sitiowebb** is an ASP.NET Core 10 Razor Pages + SignalR application for managing employee vacation and unavailability requests. It combines a manager approval workflow with real-time notifications via SignalR.

### Tech Stack
- **Framework**: ASP.NET Core 10 (net10.0) with Razor Pages + SignalR
- **Database**: SQLite locally (via EF Core), PostgreSQL in production (Npgsql)
- **Auth**: ASP.NET Identity with role-based access control
- **Email**: SendGrid/MailerSend API for notifications
- **UI**: Razor Pages with minimal client-side JS (see `Pages/` folder)

---

## Architecture & Data Flow

### Core Domain Models
Located in `Models/`:
- **VacationRequest**: Primary entity with `Kind` ("vacation"|"sick"|"halfday"), dates (`From`/`To` as `DateTimeOffset`), and `Status` (enum: Pending/Approved/Denied)
- **Unavailability**: Half-day/full-day blocks with `IsHalfDay` (stored as int in DB)
- **ApplicationUser**: Extends `IdentityUser` with `CountryCode` and `TimeZoneId` (IANA format, e.g., "Europe/Madrid")

### Database Context
`Data/ApplicationDbContext.cs`: Custom value converter for `Unavailability.IsHalfDay` (bool ↔ int) due to SQLite limitations. Always use `DateTimeOffset` for dates to handle timezones correctly.

### Request Workflow
1. User creates `VacationRequest` via Pages (status = Pending)
2. Managers approve/deny via ManagerOnly pages → triggers SignalR notifications
3. User receives real-time updates via SignalR (see `NotificationsHub`)

### Real-Time Communication
- **SignalR Hub**: `Data/Hubs/NotificationsHub.cs`
  - Groups: "managers" (auto-assigned on connection if user has Manager role)
  - UserIdProvider: `EmailUserIdProvider` uses email as connection identifier
  - **Pattern**: Managers broadcast to "managers" group, individual users targeted by email

---

## Key Services & Patterns

### Authentication & Authorization
- **Config**: `Program.cs` lines 40-58 establish Identity with strict password requirements
- **Role-Based Access**: "Manager" role controls access to `/ManagerOnly/*` pages and API endpoints
- **Convention-Based**: Some pages authorized via `options.Conventions.AuthorizePage()` in `Program.cs`

### Email Service
- **Interface**: `Services/IAppEmailSender.cs`
- **Implementations**: 
  - `MailerEmailSender`: Primary (MailerSend API)
  - `AppNullEmailSender`: Dev/test (no-op)
- **Configuration**: `EmailSettings` in appsettings → can be overridden by env vars (`EMAILSETTINGS__*`)
- **Usage**: Injected into Razor Pages codebehind files (e.g., `Pages/VacationRequest.cshtml.cs`)

### API Controllers
Located in `Controllers/`:
- **NotificationsApiController**: GET endpoints for dashboard notifications (`/api/notifications/*`)
- **PendingApiController**: Fetch pending requests for managers
- **MyDecisionApiController**: User's own decision history
- **ManagerEmployeesController**: Employee list for manager view
- **DebugNotificationsController**: Test/debug endpoint (remove before production)

---

## Development Workflows

### Local Development
```bash
# Build and run
dotnet build
dotnet run --project Sitiowebb.csproj
# Runs on http://localhost:5232 (see launchSettings.json)

# Apply migrations
dotnet ef migrations add <MigrationName>
dotnet ef database update

# Test with dummy data
# Use Pages/ManagerOnly/ClearTestData.cshtml to reset
```

### Database
- **Default**: SQLite (`Data Source=app.db`)
- **Env Variable**: `ConnectionStrings__DefaultConnection` can override
- **Npgsql Switch**: `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` required for DateTimeOffset in PostgreSQL

### Configuration Hierarchy
1. `appsettings.json` (base)
2. `appsettings.Development.json` (local overrides)
3. Environment variables (production only, not in Development)
4. `wwwroot/appsettings.Production.json` (if deployed)

---

## Project-Specific Conventions

### Email Pattern
Always use `User.FindFirstValue(ClaimTypes.Email)` or fallback to `User.Identity?.Name` (both normalized to lowercase) to get the authenticated user's email/ID.

### DateTime Handling
- **Always use `DateTimeOffset`** for VacationRequest.From/To and other timestamps
- Convert to user's timezone in Razor Pages using `ApplicationUser.TimeZoneId`
- Store UTC in database; convert client-side for display

### Role Naming
- "Manager" role: Full access to approval workflow and employee data
- Unauthenticated: Limited to public pages (Index, Privacy, Error)

### SignalR Group Management
- Managers auto-join "managers" group on connection
- Broadcast to group: `await Clients.Group("managers").SendAsync("MethodName", data)`
- Target individual: `await Clients.User(userEmail).SendAsync("MethodName", data)` (email normalized by EmailUserIdProvider)

### Unavailability Model
- **Note**: `IsHalfDay` stored as int (1/0) in SQLite due to EF Core limitations
- Always query via LINQ (EF handles conversion) — never access raw DB values directly

---

## Common Tasks

### Add New Vacation Request Type
1. Update `VacationRequest.Kind` validation in `Pages/VacationRequest.cshtml.cs`
2. Add email template in `Services/EmailTemplate.cs` if notification differs
3. Update Manager decision logic in `ManagerOnly/Request.cshtml.cs`

### Send Real-Time Notification to Managers
```csharp
var hubContext = serviceProvider.GetRequiredService<IHubContext<NotificationsHub>>();
await hubContext.Clients.Group("managers").SendAsync("MethodName", payload);
```

### Add API Endpoint
- Create method in appropriate `Controllers/*Controller.cs`
- Use `[Authorize]` or `[Authorize(Roles = "Manager")]` for access control
- Return `Ok(anonymousObject)` or `BadRequest()` (common pattern in this project)

---

## Testing & Debugging

### Test Email
- Use `Pages/TestEmail.cshtml` to verify email configuration
- Check `appsettings.Development.json` for `EmailSettings` (API key required for MailerSend)

### Debug Data Reset
- Visit `/ManagerOnly/ClearTestData` to reset vacation requests (manager only)
- SQLite file at `app.db` can be deleted to start fresh

### Known Issues & Workarounds
- **Unavailability.IsHalfDay**: Due to SQLite bool limitations, always use EF Core LINQ queries (conversions handled automatically)
- **Timezone Handling**: User's `TimeZoneId` is IANA format; use `TimeZoneInfo.FindSystemTimeZoneById(user.TimeZoneId)` for conversions
