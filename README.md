# RentalApp

RentalApp is a rental and property management web application. Applicants apply for available apartment units, and Property Managers maintain properties and units and review applications. The application uses server-rendered ASP.NET Core MVC and Razor views, with SQL Server persistence and ASP.NET Core Identity authentication.

## Tech Stack

- .NET 10 and ASP.NET Core MVC
- Razor views, partial views, and ViewComponents
- Entity Framework Core 10 with the SQL Server provider and code-first migrations
- ASP.NET Core Identity with cookie authentication
- Bootstrap, jQuery, and jQuery Validation
- Bogus for sample data
- xUnit for unit and SQL Server integration tests
- Swashbuckle for OpenAPI and Swagger UI

## Solution Structure

```text
RentalApp.sln
README.md
src/
  RentalApp.Domain/
  RentalApp.Application/
  RentalApp.Infrastructure/
  RentalApp.Web/
tests/
  RentalApp.UnitTests/
  RentalApp.IntegrationTests/
```

- **RentalApp.Domain**: entities, enums, business rules, and result types.
- **RentalApp.Application**: application use cases, commands, queries, DTOs, and repository abstractions.
- **RentalApp.Infrastructure**: EF Core, SQL Server, Identity, repositories, migrations, and database seeding.
- **RentalApp.Web**: the startup project; MVC controllers, Razor views, partials, ViewComponents, view models, and web assets.
- **RentalApp.UnitTests**: domain rules and application services tested with repository fakes.
- **RentalApp.IntegrationTests**: SQL Server persistence, migrations, seeding, and concurrent operations.

## Architecture

The production projects have these direct project references:

```text
Web            -> Application, Domain, Infrastructure
Infrastructure -> Application, Domain
Application    -> Domain
Domain         -> no other solution projects
```

Web handles MVC/Razor presentation and HTTP authorization. Application orchestrates use cases, Domain owns business rules, and Infrastructure implements persistence and Identity. Dependencies are registered through the Application and Infrastructure service-registration extensions called from `Program.cs`.

## Mandatory Functionality

The following describes the implementation of the assessment's mandatory functionality. Optional extensions are identified separately below. Remaining manual acceptance steps are noted at the end of this README.

**Applicants** can register, log in, log out, browse available units, and start an application. The application wizard contains Applicant Information, Residence History, and Summary. Applicants can maintain residence records, submit a completed application, correct and resubmit a Returned application, and withdraw applications in Draft, Submitted, or Returned status.

**Property Managers** can create, edit, and delete properties and units through modals, subject to checks that protect existing applications and leases. They can filter applications by status and property, inspect application details and status history, and approve, return, or deny an application through a review modal. Returning or denying an application requires a comment. Managers cannot edit applicant sections. The optional review-queue workflow adds the claim prerequisite described below.

**Business rules and presentation:**

- Server-side role and application-membership checks restrict access; form mutations use antiforgery validation.
- Application status/property filtering and applicant visibility are applied in the database. Applicants see applications they belong to; Property Managers see all applications. Paging and sorting are optional extensions.
- The mandatory statuses are Draft, Submitted, Returned, Approved, Denied, and Withdrawn; the last three are terminal. UnderReview is added by the optional review queue.
- A unit is unavailable when a lease includes the current UTC date. Availability is checked when starting, submitting, and approving an application.
- Approval creates a lease starting on the current UTC date and ending one day before its twelve-month anniversary. The lease, decision, and history are saved in a serializable transaction.
- Inactive unit types may remain assigned to existing units but cannot be newly assigned to other units.
- Modal forms return Razor partials with server-side validation feedback. Successful modal posts close the dialog and refresh only the affected page fragment (property list, unit list, application detail, manager notes, or application grid). ViewComponents provide the application grid, application header, and status badges.

## Optional Bonus Features

- **Bonus 1 — paging, sorting, and reusable grid:** sorting, paging, and the filtered total are calculated in SQL. Supported page sizes are 10, 20, and 50. An `ApplicationGrid` ViewComponent loads rows from the authenticated `GET /api/applications/grid` JSON endpoint, documented through OpenAPI. Database filtering itself is mandatory.
- **Bonus 2 — review queue:** claiming a Submitted application changes it to UnderReview. Only the claiming manager can complete the review or release it back to Submitted. Claim, release, and review transitions appear in status history.
- **Bonus 3 — Property Manager notes:** managers can add and edit application notes. Notes are restricted to managers and excluded from applicant-facing application DTOs.
- **Bonus 4 — incomplete section drafts:** applicant and residence data can be saved with validation errors within storage limits. This optional behavior extends the baseline rule that Continue saves only valid sections; advancing still requires valid data. Shared section rules return field errors, Summary identifies blockers, and submission revalidates the saved data. Invalid saves return a refreshed section version so a corrected retry can succeed when the section was not changed concurrently.
- **Bonus 5 — multiple applicants:** membership checks permit every applicant on an editable application to view and edit its shared sections. Server endpoints support adding existing Applicant accounts by email and removing co-applicants; the creator cannot be removed. Co-applicant Add/Remove forms are sibling forms outside the main wizard form on Summary.
- **Bonus 5a — per-section optimistic concurrency:** Applicant Information and Residence History use separate version checks. Stale saves are rejected with a reload message, while updates to different sections can succeed independently. SQL concurrency coverage is included in the integration test project.

## Prerequisites

- .NET 10 SDK.
- Windows with SQL Server Express LocalDB for the checked-in configuration. The expected instance is `(localdb)\MSSQLLocalDB`.
- A SQL Server identity with permission to create the application database, apply migrations, and write seed data. Integration tests additionally require permission to create and drop isolated test databases.

An accessible SQL Server or SQL Server Express instance can be used instead by changing the connection string. The repository does not require a separate frontend build.

## Configuration

Both `src/RentalApp.Web/appsettings.json` and `src/RentalApp.Web/appsettings.Development.json` define `ConnectionStrings:DefaultConnection`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=RentalAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
```

This is a local development configuration using Windows authentication, with no SQL username or password. It is intended to work unchanged when the LocalDB instance is installed and accessible to the Windows account running the application.

The launch profiles select the Development environment, so `appsettings.Development.json` overrides `appsettings.json`. If your SQL Server instance differs, update `DefaultConnection` in the Development file; changing only the base file will not change the Development connection. Configure the instance, authentication, and certificate settings appropriate to your server. For environment-based configuration, the corresponding key is `ConnectionStrings__DefaultConnection`; keep private credentials out of committed files.

## Getting Started

From the repository root, which contains `RentalApp.sln`:

```bash
dotnet restore
dotnet build
dotnet run --project src/RentalApp.Web/RentalApp.Web.csproj
```

The default `http` launch profile configures [http://localhost:5290](http://localhost:5290). Open that address after the console reports that the application is listening. The separate `https` profile configures `https://localhost:7161` and the same HTTP address.

Before accepting requests, `Program.cs` calls `DatabaseSeeder.MigrateAndSeedAsync`. That method applies pending EF Core migrations and then seeds the database. A separate manual migration command is not part of the normal startup sequence. SQL Server must be accessible for startup to complete.

Sign in using one of the assessment accounts below. Stop the application with Ctrl+C.

## Assessment Accounts and Roles

The seeded Identity roles are `Applicant` and `PropertyManager`. All five newly seeded accounts use the assessment password **`Passw0rd!`**:

- **Property Managers:** `pm1@rental.local` (Pat Manager), `pm2@rental.local` (Morgan Manager).
- **Applicants:** `applicant1@rental.local` (Alex Applicant), `applicant2@rental.local` (Blake Applicant), `applicant3@rental.local` (Casey Applicant).

These are local demonstration credentials defined in `DatabaseSeeder`. Existing accounts are reused without resetting their passwords. Registration also allows choosing either Applicant or Property Manager.

## Database Migrations and Seeding

Migrations are stored in `src/RentalApp.Infrastructure/Persistence/Migrations`. They cover the initial schema, manager notes, review claims, incomplete section drafts, and application membership with section versions.

For a fresh database, the seeder creates:

- The two roles and five accounts listed above.
- Three active unit types (Studio, Apartment, Townhome) and one inactive type (Loft (Legacy)).
- Three properties and twelve units, including a unit assigned the inactive type.
- Eight applications covering Draft, Submitted, UnderReview, Returned, Approved, Denied, and Withdrawn statuses, with sample residence and status-history records.
- One active lease linked to an Approved application.
- A shared Draft application for `applicant1@rental.local` and `applicant2@rental.local`; the seeded UnderReview application is claimed by `pm1@rental.local`.

Bogus generates sample property and applicant-related data using a fixed random seed; dates are relative to the seeding date. Seeding runs in every environment. It ensures missing roles, users, and role membership, inserts unit types only when their table is empty, and skips sample properties, units, applications, and leases once any property exists. Consequently, normal subsequent starts reuse existing data; this is not a repair or reset mechanism for partially populated databases.

## Tests

Run both test projects from the repository root:

```bash
dotnet test
```

Unit tests cover application rules, lease availability, section validation, application commands, co-applicants, and manager notes. Integration tests use real SQL Server to exercise filtering and paging, persistence, approval transactions, concurrent claims and section saves, and running migrations and seeding twice without duplicating the checked seed data.

Integration tests default to `(localdb)\MSSQLLocalDB`. Each test database receives a generated `RentalAppTests_<guid>` name and is deleted on disposal. The helper replaces the database name even when a custom connection string is supplied; it does not target `RentalAppDb`.

To use another test SQL Server, set `RENTALAPP_TEST_SQLSERVER` to a connection string for that server in the test process environment. This setting is independent of the Web project's connection string.

## OpenAPI

Swagger UI is configured at `/swagger`, with the OpenAPI document at `/swagger/v1/swagger.json`. The application grid endpoint uses the same Identity session cookie as the MVC application: sign in through the website before calling it from the same browser. Unauthenticated grid requests receive HTTP 401. Swagger middleware is enabled in all environments.

## Known Assessment Gaps

None at source level for the previously recorded modal-refresh, nested Summary form, and invalid-draft version-token issues. Those are covered by MVC/Razor regression tests and the controller/view/JavaScript changes described above. A full browser walkthrough and video acceptance pass remain manual next steps.

## Verification Status

Verified on this machine with .NET SDK 10 and `(localdb)\MSSQLLocalDB`:

- `dotnet restore`, `dotnet build`, and `dotnet build --no-restore` succeeded with 0 warnings and 0 errors.
- Unit tests: 114 passed (includes 15 MVC/Razor modal/wizard regression cases).
- Integration tests: 13 passed against LocalDB (migrations, seeding, persistence, and concurrency coverage).
- `dotnet run --project src/RentalApp.Web/RentalApp.Web.csproj --no-build` applied pending migrations, ran seed checks, listened on `http://localhost:5290`, and returned HTTP 200 for `/`.

This does not replace a manual browser acceptance walkthrough of every modal and wizard path.
