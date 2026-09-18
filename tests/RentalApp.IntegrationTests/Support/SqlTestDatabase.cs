using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RentalApp.Domain.Entities;
using RentalApp.Domain.Enums;
using RentalApp.Infrastructure.Persistence;

namespace RentalApp.IntegrationTests.Support;

internal sealed class SqlTestDatabase : IAsyncDisposable
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly string _databaseName;

    private SqlTestDatabase(string connectionString)
    {
        ConnectionString = connectionString;
        _databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
    }

    public string ConnectionString { get; }

    public static async Task<SqlTestDatabase> CreateAsync(bool migrate = true)
    {
        var configured = Environment.GetEnvironmentVariable("RENTALAPP_TEST_SQLSERVER")
            ?? @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=30";
        var builder = new SqlConnectionStringBuilder(configured)
        {
            InitialCatalog = "RentalAppTests_" + Guid.NewGuid().ToString("N")
        };
        var database = new SqlTestDatabase(builder.ConnectionString);
        if (migrate)
        {
            await using var db = database.CreateContext();
            await db.Database.MigrateAsync();
        }
        return database;
    }

    public AppDbContext CreateContext() => new(_options);

    public async Task<TestGraph> AddApplicationsAsync(params (string User, ApplicationStatus Status, int Property)[] specs)
    {
        await using var db = CreateContext();
        var type = new UnitType { Name = "Apartment", IsActive = true };
        var first = new Property { Name = "North", AddressLine1 = "1 North St", City = "City", State = "ST", PostalCode = "10001" };
        var second = new Property { Name = "South", AddressLine1 = "2 South St", City = "City", State = "ST", PostalCode = "10002" };
        db.AddRange(type, first, second);
        await db.SaveChangesAsync();
        var units = new[]
        {
            new Unit { PropertyId = first.Id, UnitTypeId = type.Id, UnitNumber = "101", Bedrooms = 1, MonthlyRent = 1000 },
            new Unit { PropertyId = second.Id, UnitTypeId = type.Id, UnitNumber = "201", Bedrooms = 2, MonthlyRent = 1500 }
        };
        db.Units.AddRange(units);
        await db.SaveChangesAsync();
        var applications = specs.Select((s, index) => new RentalApplication
        {
            ApplicantUserId = s.User,
            UnitId = units[s.Property - 1].Id,
            Status = s.Status,
            CurrentSection = ApplicationWizardSection.Summary,
            FullName = $"Applicant {index + 1}",
            Phone = "555-0100",
            Email = $"app{index + 1}@example.test",
            CurrentAddress = "Current address",
            ApplicantInfoSaved = true,
            ResidenceHistorySaved = true,
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(index)
        }).ToList();
        foreach (var app in applications)
            app.Applicants.Add(new ApplicationApplicant { UserId = app.ApplicantUserId, AddedAtUtc = DateTime.UtcNow });
        db.RentalApplications.AddRange(applications);
        await db.SaveChangesAsync();
        return new TestGraph(first.Id, second.Id, units[0].Id, units[1].Id, applications.Select(a => a.Id).ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        if (!_databaseName.StartsWith("RentalAppTests_", StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to delete non-test database '{_databaseName}'.");

        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }
}

internal record TestGraph(int FirstPropertyId, int SecondPropertyId, int FirstUnitId, int SecondUnitId, int[] ApplicationIds);
