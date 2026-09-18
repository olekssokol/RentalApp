# RentalApp

ASP.NET Core MVC application for rental properties, units, and tenant applications.

## Structure

```
RentalApp/
├── RentalApp.sln
├── src/
│   ├── RentalApp.Domain/
│   ├── RentalApp.Application/
│   ├── RentalApp.Infrastructure/
│   └── RentalApp.Web/
├── tests/
│   ├── RentalApp.UnitTests/
│   └── RentalApp.IntegrationTests/
├── README.md
└── .gitignore
```

Visual Studio Solution Explorer uses the same solution folders:

```
Solution 'RentalApp'
├── src
│   ├── RentalApp.Application
│   ├── RentalApp.Domain
│   ├── RentalApp.Infrastructure
│   └── RentalApp.Web
└── tests
    ├── RentalApp.UnitTests
    └── RentalApp.IntegrationTests
```

- **Domain** — entities, enums, domain services, and `Result` types
- **Application** — use cases, commands, queries, and repository interfaces
- **Infrastructure** — EF Core, Identity, and repository implementations
- **Web** — MVC controllers, views, and view models

## Run

Install the .NET 10 SDK and SQL Server Express LocalDB on Windows. From the repository root, restore and build the canonical `RentalApp.sln`, then start the web application:

```bash
dotnet restore
dotnet build --no-restore
dotnet run --project src/RentalApp.Web/RentalApp.Web.csproj
```

The app uses LocalDB (`RentalAppDb`) and seeds demo data on startup.

The application list is loaded from `GET /api/applications/grid`. The endpoint applies role visibility, filters, sorting, and paging in SQL and returns the current rows plus `filteredTotal`. Interactive API documentation is available at `/swagger` while the app is running.

Property Manager application details include private, editable manager notes. Their endpoints require the Property Manager role, and applicant-facing application contracts do not contain note data.

Demo password: `Passw0rd!`

- Property managers: `pm1@rental.local`, `pm2@rental.local`
- Applicants: `applicant1@rental.local`, `applicant2@rental.local`, `applicant3@rental.local`

## Test

The integration tests create isolated databases named `RentalAppTests_<guid>` in LocalDB and delete them when each test ends. They never use `RentalAppDb`.

```bash
dotnet test
```

To use another SQL Server instance, set `RENTALAPP_TEST_SQLSERVER` to a connection string. The test runner replaces its database name with a generated isolated name.

