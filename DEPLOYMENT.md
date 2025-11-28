# SOC-Calendar Deployment Guide

## Quick Setup with Railway.app (Recommended)

Railway is the easiest option for deploying ASP.NET Core apps with PostgreSQL.

### Step 1: Create Railway Account
1. Go to [railway.app](https://railway.app)
2. Sign up with GitHub
3. Create a new project

### Step 2: Add PostgreSQL Database
1. In your Railway project, click **+ New**
2. Select **PostgreSQL**
3. Railway creates the database and provides `DATABASE_URL`

### Step 3: Connect GitHub Repository
1. Click **+ New** → **GitHub Repo**
2. Select **seehigh/SOC-Calendar**
3. Railway auto-deploys on every push to `main`

### Step 4: Configure Environment Variables
In Railway Dashboard → Your App → Variables, add:

```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=YOUR_DATABASE_URL
EmailSettings__ApiKey=your_mailersend_api_key
EmailSettings__From=your-verified-email@mailersend.com
EmailSettings__FromName=SOC-Calendar Notifications
```

**To get `DATABASE_URL`:**
- Go to PostgreSQL service in Railway
- Click the database
- Copy the **Connection String** (Postgres format)
- Example: `Server=host;Port=5432;Database=railway;User Id=postgres;Password=pwd;`

**Email Service:**
- Use MailerSend API (already configured in code)
- Get API key from [mailersend.com](https://mailersend.com)
- Verify a sender email in MailerSend dashboard

### Step 5: Run Database Migrations
The first deployment will fail because the database is empty. After Railway deploys:

1. Clone your repo locally:
   ```bash
   git clone https://github.com/seehigh/SOC-Calendar.git
   cd SOC-Calendar
   ```

2. Install EF Core CLI:
   ```bash
   dotnet tool install --global dotnet-ef
   ```

3. Update the connection string in `Program.cs` or use environment variable:
   ```bash
   export ConnectionStrings__DefaultConnection="Server=your-host;Port=5432;Database=railway;User Id=postgres;Password=pwd;"
   dotnet ef database update
   ```

4. Or use Railway's shell to run migrations:
   - Click your app in Railway Dashboard
   - Go to **Deploy** → **Deployments** → Latest
   - Click **View Logs**
   - SSH into the container and run:
     ```bash
     dotnet ef database update
     ```

### Step 6: Verify Deployment
1. Railway provides a public URL (usually `https://soc-calendar-*.railway.app`)
2. Visit the URL and log in with:
   - Email: `saramorerasuarez@gmail.com`
   - Password: (from your appsettings.json)

---

## Alternative: Docker + Heroku

### Prerequisites
- Docker installed
- Heroku account (heroku.com)
- Heroku CLI installed

### Steps
```bash
# Login to Heroku
heroku login

# Create new app
heroku create soc-calendar

# Add PostgreSQL
heroku addons:create heroku-postgresql:essential-0

# Set environment variables
heroku config:set ASPNETCORE_ENVIRONMENT=Production
heroku config:set EmailSettings__ApiKey=your_key
heroku config:set EmailSettings__From=your_email

# Push code
git push heroku main

# Run migrations
heroku run "dotnet ef database update"
```

---

## Critical Production Checklist

- [ ] **Change default manager password** in appsettings.json before deploying
- [ ] **Update email credentials** (MailerSend API key)
- [ ] **Use environment variables** for all secrets (API keys, passwords)
- [ ] **Enable HTTPS** (Railway/Heroku handle this automatically)
- [ ] **Set ASPNETCORE_ENVIRONMENT=Production**
- [ ] **Run `dotnet ef database update`** to create tables
- [ ] **Test email sending** via `/TestEmail` page (remove before production)
- [ ] **Remove debug endpoints** from `Program.cs` (DebugNotificationsController)
- [ ] **Configure CORS** if using separate frontend
- [ ] **Set up backup strategy** for PostgreSQL database
- [ ] **Configure custom domain** (optional)

---

## Troubleshooting

### Database Connection Issues
```bash
# Test connection locally
psql "postgresql://user:password@host:port/database"

# Verify EF Core can connect
dotnet ef migrations list
```

### Email Not Sending
1. Check MailerSend API key is correct
2. Verify sender email is verified in MailerSend
3. Check Railway logs: `heroku logs --tail` or Railway Dashboard

### App Not Starting
1. Check environment variables are set
2. Verify connection string format (PostgreSQL uses `;` not `,`)
3. View logs: Railway Dashboard → Deployments → View Logs

### SignalR Connection Issues
- Ensure WebSocket support is enabled (Railway supports this)
- Check browser console for connection errors

---

## Local Development Setup

```bash
# Install dependencies
dotnet restore

# Set up SQLite database
dotnet ef database update

# Run app
dotnet run --project Sitiowebb.csproj

# Open browser
open http://localhost:5232
```

---

## Production Database Schema

The app will create these tables automatically via EF Core migrations:
- `AspNetUsers` - User accounts
- `AspNetRoles` - "Manager" and "Usuario" roles
- `AspNetUserRoles` - Role assignments
- `VacationRequests` - Vacation/sick/half-day requests
- `Unavailabilities` - Employee unavailability blocks

---

## Monitoring & Maintenance

### View Logs
- **Railway**: Dashboard → Deployments → View Logs
- **Heroku**: `heroku logs --tail`

### Database Backups
- **Railway**: Automatic daily backups included
- **Heroku**: Use `heroku pg:backups` commands

### Scale Up
- Railway: Increase CPU/Memory in dashboard
- Heroku: Change dyno type to higher tier

---

## Need Help?

- Railway Docs: https://docs.railway.app
- ASP.NET Core Deployment: https://docs.microsoft.com/aspnet/core/host-and-deploy
- PostgreSQL Setup: https://www.postgresql.org/docs/
