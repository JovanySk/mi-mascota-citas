# Mi Mascota · Veterinary appointment booking

![CI](../../actions/workflows/ci.yml/badge.svg)

Appointment booking system for a small veterinary clinic, **Mi Mascota**. Staff register pet owners and their pets, keep a catalog of services, and book appointments on a calendar grid that only offers free time slots. Each appointment then moves through a controlled workflow: **Pendiente → Confirmada → Atendida**, or **Cancelada**.

**Stack:** C# · ASP.NET Core 9 MVC (Razor views) · Entity Framework Core 9 · SQL Server (SQLite for local runs) · Bootstrap 5 · jQuery/AJAX · xUnit

The UI is in Spanish (the clinic's language). The code is in English.

## Features

- **Owners, pets and services**: CRUD with server-side and client-side validation. Records that are still in use can't be deleted (an owner with pets, a pet or service with appointments); the app explains why.
- **Appointments**: list filtered by date and status, plus one button for each status the appointment can move to next.
- **Dynamic availability**: in the booking form, choosing a service and a date calls `GET /Appointments/Availability` with jQuery AJAX. The free slots come back as JSON and are drawn as buttons, with no page reload.
- **Double-booking protection**: the server checks the slot again before saving. If it overlaps another appointment, the form shows a descriptive message, e.g. *"El horario 12:30–13:30 ya está ocupado: se empalma con la cita de Rocky (Baño y estética, 13:00–14:00)."*
- **Controlled state changes**: invalid transitions are rejected with a Spanish message that explains why.
- **Demo data**: 8 owners, 14 pets (dogs of six breeds, cats, guinea pigs, hamsters and rabbits), 6 services and 14 appointments on today and the next open day, covering all four statuses.

## Screenshots

**Inicio** (landing + the real agenda of the day, full page):

![Inicio](docs/screenshots/inicio.png)

| Citas | Nueva cita (slots loaded via AJAX) |
|---|---|
| ![Citas](docs/screenshots/citas.png) | ![Nueva cita](docs/screenshots/nueva-cita.png) |

| Mascotas | Dueños |
|---|---|
| ![Mascotas](docs/screenshots/mascotas.png) | ![Dueños](docs/screenshots/duenos.png) |

| Servicios | Inicio on mobile (390 px) |
|---|---|
| ![Servicios](docs/screenshots/servicios.png) | ![Inicio en móvil](docs/screenshots/inicio-movil.png) |

## Diseño

The UI follows a design made in Figma for this project ("Mi Mascota · Inicio"): a playful clinic landing page whose panels show the real system data.

- **Tokens** (CSS variables in `wwwroot/css/site.css`): turquoise `#12CED6` for fills, giant words and calls to action; deep turquoise `#0A9AA2` for turquoise text on light backgrounds; ink `#3E4347`; page `#EEF0F1`; muted `#7A8086`; footer `#1E2A2E`. Each status has its own badge colors (Pendiente amber, Confirmada turquoise, Atendida green, Cancelada red). Pill buttons, cards with 22–32 px radius and a soft shadow.
- **Fonts**: *Baloo 2* ExtraBold for headings, the logo and the giant background words; *Montserrat* 500/600/700 for text. Both are self-hosted in `wwwroot/fonts` (no font CDN), and so are the Bootstrap Icons.
- **Real data in the landing**: the hero cards show today's appointments and how many are still pending, plus the opening hours from `appsettings.json`. "Agenda de hoy" lists today's appointments with the same state-transition buttons as the list. "Nueva cita rápida" calls `GET /Appointments/Availability` for the grooming service on the next open day, draws taken slots struck through and links to the booking form with the slot preselected. The bath banner reads the price and duration of the "Baño y estética" service.
- **Pet avatars**: `PetAvatar` picks an illustration from the pet's species and breed (perro → breed-specific dog, gato, cuyo, hámster, conejo), so every list shows a face.
- **Animations** (CSS + a little vanilla JS, all disabled with `prefers-reduced-motion`): the golden puppy waves, the kitten rises from behind the headline and then bobs, the bones float, giant words slide in from the right, the "todos" pets hop one after another when the section scrolls into view (`IntersectionObserver`), team cards lift on hover and the hero numbers count up.
- **Images**: generated with Figma AI, backgrounds removed with rembg, converted to WebP. See [docs/CREDITS.md](docs/CREDITS.md).

## Domain rules

### Appointment state machine

| From \ To      | Pendiente | Confirmada | Atendida | Cancelada |
|----------------|:---------:|:----------:|:--------:|:---------:|
| **Pendiente**  | –         | ✅ ¹       | ❌       | ✅        |
| **Confirmada** | ❌        | –          | ✅       | ✅        |
| **Atendida**   | ❌        | ❌         | –        | ❌        |
| **Cancelada**  | ❌        | ❌         | ❌       | –         |

¹ An appointment whose start time has already passed can't be confirmed.

New appointments always start as **Pendiente**. *Atendida* and *Cancelada* are final states. Every rejected change returns a specific message, for example:

- Pendiente → Atendida: *"No se puede marcar como atendida una cita pendiente: primero debe confirmarse."*
- Atendida → anything: *"La cita ya fue atendida; su estado ya no puede cambiar."*
- Cancelada → anything: *"La cita está cancelada; su estado ya no puede cambiar…"*

The rules live in a single place, [`AppointmentStateMachine`](src/MiMascota.Web/Services/AppointmentStateMachine.cs). The service uses it to validate a change, and the list view uses it to decide which buttons to show.

### Availability and booking rules

| Rule | Detail |
|---|---|
| Opening hours | 09:00–18:00, Monday to Saturday (configurable) |
| Grid | Appointments start every 30 minutes (configurable) |
| Duration | Each service has its own duration. A slot is only offered if the whole service ends by closing time (a 90-minute service's last slot is 16:30). |
| Overlaps | One consultation room: no two active appointments can overlap. Two ranges overlap when `a.Start < b.End && a.End > b.Start`, so back-to-back appointments are allowed. |
| Cancelled | A cancelled appointment frees its slot. |
| Closed days | Sundays return *"La clínica no abre los domingos."* |
| Past | No past dates, and on the current day no slot that has already started. |

The hours are set in `appsettings.json`:

```json
"Clinic": {
  "OpeningTime": "09:00",
  "ClosingTime": "18:00",
  "SlotMinutes": 30,
  "ClosedDays": [ "Sunday" ]
}
```

## Architecture

```
Browser (Razor views + Bootstrap + jQuery)
        │  HTTP / AJAX (JSON)
        ▼
Controllers ── thin: bind input, call a service, choose a view/redirect
        │
        ▼
Services ──── business rules, return Result / Result<T> (no exceptions for rule violations)
  ├─ AvailabilityService   free slots for a date + service
  ├─ AppointmentService    booking with overlap check, status changes
  ├─ AppointmentStateMachine  allowed transitions + Spanish messages
  └─ ClinicSchedule        opening hours, closed days, slot grid (pure logic)
        │
        ▼
EF Core (AppDbContext) ── SQL Server (migrations) or SQLite (EnsureCreated)
```

```
src/MiMascota.Web/
  Controllers/   Home, Appointments, Owners, Pets, Services
  Services/      Result, ClinicSchedule, AvailabilityService, AppointmentService, AppointmentStateMachine
  Models/        Owner, Pet, VetService, Appointment, AppointmentStatus (+ Spanish display names)
  ViewModels/    Form and list models for the views
  Data/          AppDbContext, SeedData, DatabaseSetup, Migrations (SQL Server)
  Views/         Razor views (Spanish UI)
  wwwroot/js/    appointment-create.js (AJAX availability)
tests/MiMascota.Tests/
  AvailabilityServiceTests, AppointmentServiceTests, WebIntegrationTests
```

Design decisions:

- **Business errors are values, not exceptions.** `AppointmentService.CreateAsync` returns `Result<Appointment>`. The controller either redirects or adds the message to `ModelState` and shows the form again.
- **The end time is stored.** `End` is calculated once from the service duration when the appointment is booked. Overlap checks are then a plain SQL query, and changing a service's duration later doesn't move existing appointments.
- **Time is injected.** The services use `TimeProvider`, so tests can freeze "now" to check the "no past slots" and "can't confirm in the past" rules.
- **CRUD controllers use the `DbContext` directly.** Owners, pets and services have no business rules beyond validation, so an extra layer would only add indirection. Everything about booking goes through the services.

## Running locally

Requirements: [.NET 9 SDK](https://dotnet.microsoft.com/download).

### Option 1: SQLite (zero setup)

```bash
cd src/MiMascota.Web
dotnet run
```

Open http://localhost:5080. In Development, `appsettings.Development.json` sets `Database:Provider = "Sqlite"`. The app creates `mimascota.db` with `EnsureCreated()` and loads the demo data. To start from scratch, delete `mimascota.db`.

### Option 2: SQL Server

`appsettings.json` uses the SQL Server provider with a placeholder password. Supply the real connection string through an environment variable (or `dotnet user-secrets`). Never commit it.

```bash
# PowerShell: $env:ConnectionStrings__SqlServer = "..."
export Database__Provider=SqlServer
export ConnectionStrings__SqlServer="Server=localhost,1433;Database=MiMascota;User Id=sa;Password=<your-password>;TrustServerCertificate=True"
cd src/MiMascota.Web
dotnet run
```

On startup the app applies the EF Core migrations in `Data/Migrations` (`Database.Migrate()`) and seeds the demo data if the database is empty.

**Database approach:** SQL Server is the "real" database and is versioned with **migrations**. SQLite is only for quick local runs and tests, so it uses `EnsureCreated()` from the same model. Migrations are always generated for SQL Server through `DesignTimeDbContextFactory`:

```bash
dotnet ef migrations add <Name> --project src/MiMascota.Web -o Data/Migrations
```

### Option 3: Docker

```bash
# App + SQL Server 2022 (create a .env file with MSSQL_SA_PASSWORD=YourStrong!Passw0rd first)
docker compose up --build
# → http://localhost:8080

# Or only the app, with SQLite inside the container
docker build -t mi-mascota .
docker run -p 8080:8080 -e Database__Provider=Sqlite mi-mascota
```

## Tests

```bash
dotnet test
```

62 test cases (30 test methods) with xUnit:

- **AvailabilityServiceTests**: SQLite in-memory database and a fixed clock. Covers the 30-minute grid, durations of 60/90 minutes, overlaps, cancelled appointments freeing slots, Sundays, past dates, no past slots today, unknown service, and hours read from configuration.
- **AppointmentServiceTests**: covers booking (end time, overlap rejection for exact, partial-start and partial-end overlaps, back-to-back, re-using a cancelled slot, schedule violations) and every valid and invalid status transition (4 + 12 cases). Also checks that an appointment can't be confirmed in the past.
- **PetAvatarTests**: the avatar chosen for each species and breed, and the fallback for unknown species.
- **WebIntegrationTests**: `WebApplicationFactory` with a temporary SQLite file. Checks that every page renders, that the availability endpoint returns JSON slots or the closed-day message, that posting an overlapping appointment re-renders the form with the error, that a valid post redirects, and that a status change without an antiforgery token is rejected.

CI (GitHub Actions) runs the same command on every push and pull request.

---

Author: **Jovany Morales del Ángel** · MIT License
