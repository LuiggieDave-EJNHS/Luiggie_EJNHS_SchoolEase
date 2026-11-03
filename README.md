# SchoolEase

**Making School Management Easier.**

A centralized school management and office-management system for a Philippine senior high school, built with ASP.NET Core MVC + Entity Framework Core.

---

## 1. Tech stack

| Layer | Technology |
|-------|------------|
| Framework | ASP.NET Core 8.0 MVC (C#) |
| ORM | Entity Framework Core 8.0.10 |
| Database | SQLite (default) — MySQL / SQL Server selectable |
| Auth | ASP.NET Core Identity + Role-Based Access Control |
| UI | HTML5, CSS3, JavaScript, Bootstrap 5, Bootstrap Icons, Chart.js |
| Reporting | ClosedXML (Excel export), printable Razor report views |

---

## 2. Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- (Optional) MySQL 8.x server if you want to run on MySQL instead of SQLite
- (Optional) SQL Server / LocalDB

---

## 3. Project structure

```
School_Ease/
├─ SchoolEase.sln
└─ SchoolEase.Web/
   ├─ Program.cs                 # Host, Identity, policies, DI, migration + seed
   ├─ appsettings.json           # Provider + connection strings + seed flags
   ├─ Infrastructure/
   │   └─ DatabaseSetup.cs       # Provider-agnostic DbContext registration
   ├─ Models/
   │   ├─ Entities/              # AppUser, Student, Teacher, Section, Subject,
   │   │                         # ClassSchedule, AttendanceRecord, GradeRecord,
   │   │                         # DocumentRequest, Announcement, Notification, ...
   │   ├─ ViewModels/            # Dashboard / Academic / People / Records / Report VMs
   │   ├─ Enums.cs               # AttendanceStatus, RequestStatus, ...
   │   ├─ Roles.cs / SchoolInfo.cs
   ├─ Data/
   │   ├─ ApplicationDbContext.cs
   │   ├─ DbSeeder.cs            # Roles, accounts, system settings
   │   ├─ DemoDataSeeder.cs      # Grade levels, sections, subjects, people, schedules
   │   ├─ DemoRecordsSeeder.cs   # Attendance, grades, requests, announcements, events
   │   └─ Migrations/            # EF Core migrations (single InitialCreate)
   ├─ Services/                  # CurrentUser, Audit, Notification, Dashboard, Report
   ├─ Helpers/                   # PagedList, DisplayExtensions
   ├─ Controllers/               # One per module (23 controllers)
   └─ Views/                     # Razor views (Shared layouts for each role)
```

---

## 4. Running the app

From the repository root:

```powershell
dotnet run --project SchoolEase.Web
```

On first launch the app automatically:

1. Applies all EF Core migrations (`Database.MigrateAsync()`).
2. Seeds the six roles and system settings.
3. Creates **one administrator account** from `Seed:SuperAdminEmail` / `Seed:SuperAdminPassword`.

No demo accounts and no sample school data are created unless you explicitly opt in with
`Seed:DemoData=true` (see §9).

The default launch profile listens on `http://localhost:5123`. Use `--urls` to override:

```powershell
dotnet run --project SchoolEase.Web --urls http://localhost:5080
```

The app opens on the **login page**. Every module (dashboard statistics, attendance, grades, requests, announcements) reads from the database — nothing is static.

---

## 5. Your first account

There are **no built-in demo accounts**. On startup the app creates exactly one account —
a Super Administrator — from configuration:

| Setting | Value in `appsettings.Development.json` |
|---------|------------------------------------------|
| `Seed:SuperAdminEmail` | `admin@schoolease.ph` |
| `Seed:SuperAdminPassword` | `SchoolEase@2026` |

Sign in with those, change the password, then create every other account from
**System Settings → User Accounts**. The sign-in screen does not list any credentials.

### Roles at a glance

| Role | Scope |
|------|-------|
| Super Administrator | Full system administration, including user accounts and audit logs |
| School Administrator | Day-to-day school operations and office management |
| ICT Head Coordinator | School-wide academic oversight (teachers, sections, schedules, attendance, grades) — no system administration |
| Teacher | Attendance marking and grade encoding for assigned classes only |
| Registrar | Student records and fee-free document requests |
| Student | Personal schedule, grades, document requests |

---

## 6. Grading & document requests

**Grading periods.** The academic year is divided into **three terms — Term 1, Term 2 and Term 3** (no quarters or semesters). Each term grade uses the DepEd SHS weighting:

| Component | Weight |
|-----------|--------|
| Written Work | 30% |
| Performance Task | 50% |
| Term Assessment | 20% |

**Document requests.** Requesting a school document is **fee-free**. Each request moves through the lifecycle *Pending → Processing → Ready for Release → Released*, with a **processing time** shown instead of any amount.

---

## 7. Switching the database provider

The active provider is chosen by `Database:Provider` in [appsettings.json](SchoolEase.Web/appsettings.json) and resolved in [DatabaseSetup.cs](SchoolEase.Web/Infrastructure/DatabaseSetup.cs). Set the provider there, then supply the connection string through `ConnectionStrings:DefaultConnection`.

> `appsettings.json` intentionally holds **no credentials** — only the zero-secret SQLite
> path. Supply real connection strings through user secrets (local) or environment
> variables (hosting) so they never land in source control. Even locally, never commit
> a real connection string.

### SQLite (default — no install required)

```json
"Database": { "Provider": "Sqlite" },
"ConnectionStrings": { "DefaultConnection": "Data Source=schoolease.db" }
```

### MySQL

```json
"Database": { "Provider": "MySql", "MySqlVersion": "8.0.36" }
```

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" `
  "server=localhost;port=3306;database=schoolease_db;user=schoolease;password=<your-password>;" `
  --project SchoolEase.Web
```

### SQL Server

```json
"Database": { "Provider": "SqlServer" }
```

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" `
  "Server=(localdb)\MSSQLLocalDB;Database=SchoolEase;Trusted_Connection=True;MultipleActiveResultSets=true" `
  --project SchoolEase.Web
```

Then run the migration against the new provider:

```powershell
dotnet ef database update --project SchoolEase.Web
```

> For production, set the connection string with the environment variable
> `ConnectionStrings__DefaultConnection` instead of committing it.

> **Multi-provider note:** the committed migrations in `Data/Migrations` are SQLite-specific.
> On startup the app runs `Migrate()` when `Database:Provider` is `Sqlite` and `EnsureCreated()`
> for SQL Server / MySQL, so the schema is built from the current model on those engines.

---

## 8. Common commands

```powershell
# Restore + build
dotnet build SchoolEase.sln

# Run
dotnet run --project SchoolEase.Web

# Add a migration after changing models
dotnet ef migrations add <Name> --project SchoolEase.Web

# Apply migrations manually
dotnet ef database update --project SchoolEase.Web

# Reset the SQLite database (start fresh + reseed)
Remove-Item SchoolEase.Web\schoolease.db*
```

---

## 9. Configuration flags

| Key | Purpose |
|-----|---------|
| `Database:Provider` | `Sqlite` \| `MySql` \| `Postgres` \| `SqlServer` |
| `Database:MySqlVersion` | Server version string used by Pomelo |
| `Database:EnableSensitiveDataLogging` | EF detailed logging (dev only) |
| `Seed:Enabled` | Master switch for seeding on startup |
| `Seed:DemoData` | Opt in to the sample dataset **and** its sample accounts (default `false` — leave off on a live site) |
| `Seed:SuperAdminEmail` / `Seed:SuperAdminPassword` / `Seed:SuperAdminName` | The one administrator created on startup, in every environment |
| `DataProtection:KeysPath` | Folder for the data-protection key ring (default `<content-root>/keys`) |
| `Https:Redirect` | Set `false` when a reverse proxy terminates TLS (Render, Azure, MonsterASP before HTTPS) |
| `Mail:SmtpServer` | SMTP host used to send account-confirmation e-mails |
| `Mail:SmtpPort` | SMTP port (587 for STARTTLS, 465 for SSL) |
| `Mail:SmtpUser` | SMTP username |
| `Mail:SmtpPass` | SMTP password / app password |
| `Mail:SenderName` | Display name on outgoing e-mails |
| `Mail:SenderEmail` | From address on outgoing e-mails |

---

## 10. Security notes

- Passwords are hashed by ASP.NET Core Identity; lockout after 5 failed attempts (10-minute lockout).
- `AutoValidateAntiforgeryTokenAttribute` is applied globally — all unsafe verbs require an anti-forgery token.
- Authorization is enforced through policies (`AdminOnly`, `SuperAdminOnly`, `StaffOnly`, `TeacherOrAdmin`, `AcademicOversight`, `CanPostAnnouncements`, `CanManageRequests`).
- Key administrative actions are written to the audit log (`AuditLogs` module).
- HTTP-only, sliding-expiration auth cookie (`SchoolEase.Auth`).
- No demo accounts are seeded: the only account created on startup is the administrator from configuration.
- The data-protection key ring is persisted to disk (`<content-root>/keys`) so sign-in cookies survive app restarts and IIS app-pool recycles; keep that folder out of source control and back it up.

---

## 11. Free deployment — GitHub → Render → Neon

**GitHub Pages / Vercel cannot host this app.** They serve static files and serverless
functions only; SchoolEase is a long-running ASP.NET Core server process with a database.
GitHub is used as the *source*, and a free container host runs the app from it.

The stack below costs **$0 and needs no credit card**:

| Piece | Service | Free tier |
|-------|---------|-----------|
| Source + auto-deploy trigger | GitHub | free |
| Application host | [Render](https://render.com) Web Service (Docker) | 750 instance-hours/month, 512 MB RAM |
| Database | [Neon](https://neon.tech) PostgreSQL | 0.5 GB storage, always on |
| HTTPS + auto-deploy on push | included | free |

### Step 1 — Push the project to GitHub

```powershell
git add .
git commit -m "Add Render/PostgreSQL deployment"
git push origin main
```

The repo already contains a [`Dockerfile`](../Dockerfile) and `.dockerignore`, which is all
Render needs to build the app.

### Step 2 — Create the free Neon database

1. Sign up at [neon.tech](https://neon.tech) (GitHub sign-in works, no card).
2. **Create project** → pick the region closest to your users (e.g. Singapore).
3. Copy the **connection string**. It looks like:
   `postgresql://user:password@ep-xxx.ap-southeast-1.aws.neon.tech/neondb?sslmode=require`
4. Convert it to the Npgsql key/value form and **keep `SSL Mode=Require`**:
   ```
   Host=ep-xxx.ap-southeast-1.aws.neon.tech;Database=neondb;Username=user;Password=password;SSL Mode=Require;Trust Server Certificate=true
   ```

### Step 3 — Create the Render web service

1. Sign up at [render.com](https://render.com) with your GitHub account.
2. **New → Web Service** → connect the `Luiggie_EJNHS_SchoolEase` repository.
3. Settings:
   - **Runtime**: `Docker` (auto-detected from the `Dockerfile` in the repo root)
   - **Instance type**: `Free`
   - **Region**: the one nearest your users
4. Add the environment variables below, then click **Create Web Service**.

| Name | Value |
|------|-------|
| `Database__Provider` | `Postgres` |
| `ConnectionStrings__DefaultConnection` | the Neon string from Step 2 |
| `Seed__SuperAdminEmail` | your real administrator e-mail |
| `Seed__SuperAdminPassword` | a strong password for that account |
| `Seed__SuperAdminName` | (optional) display name |
| `Mail__SmtpServer` | e.g. `smtp.gmail.com` |
| `Mail__SmtpPort` | `587` |
| `Mail__SmtpUser` | the mailbox that sends confirmations |
| `Mail__SmtpPass` | an app password (Gmail requires an App Password, not the login password) |
| `Mail__SenderName` | `SchoolEase - Emilio Jacinto NHS` |
| `Mail__SenderEmail` | the same address as `Mail__SmtpUser` |
| `Https__Redirect` | `false` (Render terminates TLS at its proxy) |

Render builds the image, then serves the site at `https://<service-name>.onrender.com`.
The first request creates the schema and the administrator account from the variables above.

### Step 4 — Stop the free instance from sleeping

A free Render service sleeps after 15 minutes without traffic. A free uptime pinger keeps
it awake, and 750 free instance-hours comfortably cover a full month (~730 hours):

1. Create a free monitor at [cron-job.org](https://cron-job.org) or [UptimeRobot](https://uptimerobot.com).
2. Point it at `https://<service-name>.onrender.com/Account/Login`.
3. Set the interval to every **10 minutes**.

### Step 5 — Deploying updates

Every `git push` to `main` triggers an automatic rebuild and redeploy — no manual uploads.

### Troubleshooting

| Symptom | Cause / fix |
|---------|-------------|
| Build fails on Render | Check the build log; the `Dockerfile` must be in the repo root |
| `Npgsql` connection error | `Database__Provider` must be `Postgres` and the string must include `SSL Mode=Require` |
| Nobody can sign in after deploy | `Seed__SuperAdminEmail` / `Seed__SuperAdminPassword` not set — the startup log warns about this |
| Users logged out after each deploy | The container filesystem is ephemeral, so the data-protection key ring is regenerated; sign in again |
| Confirmation e-mails never arrive | `Mail__*` variables missing or wrong — check the Render logs for the "not configured" warning |
| Links in e-mails point to `http://` | The reverse-proxy headers middleware is missing — already handled in `Program.cs` |

### Alternative: MonsterASP.NET (IIS)

MonsterASP.NET also has a free plan (5 GB, one 1 GB SQL Server/MySQL database, no credit
card), but it requires publishing and uploading files manually and HTTPS is only enabled by
opening a support ticket. Use `Database__Provider=SqlServer` or `MySql` and follow the same
environment-variable names as above, with `Https__Redirect=false` until HTTPS is active.

