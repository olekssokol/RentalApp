using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentalApp.Domain.Enums;
using RentalApp.Infrastructure.Identity;
using RentalApp.Infrastructure.Persistence;
using RentalApp.IntegrationTests.Support;

namespace RentalApp.IntegrationTests.Persistence;

public class DatabaseSeederTests
{
    [Fact]
    public async Task MigrateAndSeedAsync_ExecutedTwice_DoesNotDuplicateMandatorySeedData()
    {
        await using var database = await SqlTestDatabase.CreateAsync(migrate: false);
        await using var services = BuildServices(database.ConnectionString);

        await DatabaseSeeder.MigrateAndSeedAsync(services);
        var first = await SnapshotAsync(database);
        await DatabaseSeeder.MigrateAndSeedAsync(services);
        var second = await SnapshotAsync(database);

        Assert.Equal(first, second);
        Assert.Equal(AppRoles.All.Order(), first.Roles.Order());
        Assert.Equal(new[]
        {
            "applicant1@rental.local", "applicant2@rental.local", "applicant3@rental.local",
            "pm1@rental.local", "pm2@rental.local"
        }, first.Emails.Order());
        Assert.Contains("Loft (Legacy):False", first.UnitTypes);
        Assert.NotEmpty(first.Properties);
        Assert.NotEmpty(first.Units);
        Assert.Equal(Enum.GetValues<ApplicationStatus>().Order(), first.Statuses.Distinct().Order());
    }

    private static ServiceProvider BuildServices(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
        }).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
        return services.BuildServiceProvider();
    }

    private static async Task<SeedSnapshot> SnapshotAsync(SqlTestDatabase database)
    {
        await using var db = database.CreateContext();
        var unitTypes = await db.UnitTypes
            .OrderBy(t => t.Name)
            .Select(t => new { t.Name, t.IsActive })
            .ToArrayAsync();

        return new SeedSnapshot(
            await db.Roles.Select(r => r.Name!).OrderBy(x => x).ToArrayAsync(),
            await db.Users.Select(u => u.Email!).OrderBy(x => x).ToArrayAsync(),
            unitTypes.Select(t => $"{t.Name}:{t.IsActive}").ToArray(),
            await db.Properties.Select(p => p.Id + ":" + p.Name).OrderBy(x => x).ToArrayAsync(),
            await db.Units.Select(u => u.Id + ":" + u.PropertyId + ":" + u.UnitNumber).OrderBy(x => x).ToArrayAsync(),
            await db.RentalApplications.Select(a => a.Id + ":" + a.ApplicantUserId + ":" + a.UnitId + ":" + a.Status).OrderBy(x => x).ToArrayAsync(),
            await db.RentalApplications.Select(a => a.Status).OrderBy(x => x).ToArrayAsync());
    }

    private sealed class SeedSnapshot : IEquatable<SeedSnapshot>
    {
        public SeedSnapshot(
            string[] roles, string[] emails, string[] unitTypes, string[] properties,
            string[] units, string[] applications, ApplicationStatus[] statuses)
        {
            Roles = roles;
            Emails = emails;
            UnitTypes = unitTypes;
            Properties = properties;
            Units = units;
            Applications = applications;
            Statuses = statuses;
        }

        public string[] Roles { get; }
        public string[] Emails { get; }
        public string[] UnitTypes { get; }
        public string[] Properties { get; }
        public string[] Units { get; }
        public string[] Applications { get; }
        public ApplicationStatus[] Statuses { get; }

        public bool Equals(SeedSnapshot? other) => other is not null
            && Roles.SequenceEqual(other.Roles)
            && Emails.SequenceEqual(other.Emails)
            && UnitTypes.SequenceEqual(other.UnitTypes)
            && Properties.SequenceEqual(other.Properties)
            && Units.SequenceEqual(other.Units)
            && Applications.SequenceEqual(other.Applications)
            && Statuses.SequenceEqual(other.Statuses);

        public override bool Equals(object? obj) => Equals(obj as SeedSnapshot);

        public override int GetHashCode() => HashCode.Combine(Roles.Length, Emails.Length, UnitTypes.Length, Properties.Length, Units.Length, Applications.Length, Statuses.Length);
    }
}
