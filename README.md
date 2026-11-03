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
| `Database:Provider` | `Sqlite` \| `MySql` \| `SqlServer` |
| `Database:MySqlVersion` | Server version string used by Pomelo |
| `Database:EnableSensitiveDataLogging` | EF detailed logging (dev only) |
| `Seed:Enabled` | Master switch for seeding on startup |
| `Seed:DemoData` | Opt in to the sample dataset **and** its sample accounts (default `false` — leave off on a live site) |
| `Seed:SuperAdminEmail` / `Seed:SuperAdminPassword` / `Seed:SuperAdminName` | The one administrator created on startup, in every environment |
| `DataProtection:KeysPath` | Folder for the data-protection key ring (default `<content-root>/keys`) |
| `Https:Redirect` | Set `false` while the host serves HTTP only (see deployment) |

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

## 11. Free deployment — MonsterASP.NET

[MonsterASP.NET](https://www.monsterasp.net/) hosts ASP.NET Core 8 on IIS with a **free plan**:
1 website, 5 GB storage, 256 MB RAM, one 1 GB SQL Server or MySQL database, a free
`*.runasp.net` subdomain, no ads and **no credit card**. HTTPS on the free plan is
activated by opening a support ticket.

### Step 1 — Push the project to GitHub

```powershell
git init
git add .
git commit -m "SchoolEase"
git branch -M main
git remote add origin https://github.com/<your-username>/schoolease.git
git push -u origin main
```

`.gitignore` already excludes `bin/`, `obj/`, local `*.db` files, `appsettings.Development.json` and the `keys/` key ring.

### Step 2 — Create the site and database

1. Sign up at MonsterASP.NET and open the Control Panel.
2. **Create Website** → name it `schoolease`. Note the URL it gives you, e.g.
   `https://schoolease.runasp.net`.
3. **Create Database** → SQL Server (or MySQL). Copy the full connection string.

### Step 3 — Deploy

In the website's **Deployment / Git** section, connect your GitHub repository and point the
project to `SchoolEase.Web/SchoolEase.Web.csproj`. (FTP and Web Deploy from Visual Studio
also work — publish with `dotnet publish -c Release` and upload the contents of `publish/`.)

The site root must contain the published files where `SchoolEase.Web.dll` sits directly next
to `web.config`. **`web.config` is required** — it registers the ASP.NET Core handler and
must use `hostingModel="inprocess"`.

### Step 4 — Point the app at the hosted database

Add these **Application Settings / Environment Variables** in the MonsterASP control panel
(do not commit real credentials to GitHub):

| Name | Value |
|------|-------|
| `Database__Provider` | `SqlServer` (or `MySql`) |
| `ConnectionStrings__DefaultConnection` | the connection string from Step 2 |
| `Seed__SuperAdminEmail` | your real administrator e-mail |
| `Seed__SuperAdminPassword` | a strong password for that account |
| `Seed__SuperAdminName` | (optional) display name for the account |
| `Https__Redirect` | `false` until HTTPS is activated, then remove this row |

On first request the app creates the schema and then creates the administrator above.
Sign in with that e-mail, change the password, then create everyone else from
**System Settings → User Accounts**.

### Step 5 — HTTPS

Open a support ticket requesting free Let's Encrypt HTTPS for your `*.runasp.net` site.
Once HTTPS works, delete the `Https__Redirect` setting so traffic is forced to HTTPS.

### Troubleshooting

| Symptom | Cause / fix |
|---------|-------------|
| `HTTP Error 500.30` | `web.config` missing, or wrong DLL name in `arguments` |
| `HTTP Error 500.31` | IIS hosting model mismatch — must be `inprocess` |
| Site redirects to a dead `https://` URL | Set `Https__Redirect` to `false` |
| Login works but data resets | Database connection string not applied — check Step 4 |
| Nobody can sign in after deploy | `Seed__SuperAdminEmail` / `Seed__SuperAdminPassword` not set — the startup log warns about this |
| Blank page, no detail | Read `logs/stdout*.log` under the site root |

