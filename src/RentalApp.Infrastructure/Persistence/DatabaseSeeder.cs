using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentalApp.Domain.Entities;
using RentalApp.Domain.Enums;
using RentalApp.Domain.Services;
using RentalApp.Infrastructure.Identity;
using RentalApp.Infrastructure.Persistence;

namespace RentalApp.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public const string DefaultPassword = "Passw0rd!";

    public static async Task MigrateAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.MigrateAsync();
        await SeedAsync(db, userManager, roleManager);
    }

    public static async Task SeedAsync(AppDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        if (!await db.UnitTypes.AnyAsync())
        {
            db.UnitTypes.AddRange(
                new UnitType { Name = "Studio", IsActive = true },
                new UnitType { Name = "Apartment", IsActive = true },
                new UnitType { Name = "Townhome", IsActive = true },
                new UnitType { Name = "Loft (Legacy)", IsActive = false });
            await db.SaveChangesAsync();
        }

        var managers = await EnsureUsersAsync(userManager, AppRoles.PropertyManager,
        [
            ("pm1@rental.local", "Pat Manager"),
            ("pm2@rental.local", "Morgan Manager")
        ]);

        var applicants = await EnsureUsersAsync(userManager, AppRoles.Applicant,
        [
            ("applicant1@rental.local", "Alex Applicant"),
            ("applicant2@rental.local", "Blake Applicant"),
            ("applicant3@rental.local", "Casey Applicant")
        ]);

        if (await db.Properties.AnyAsync())
            return;

        Randomizer.Seed = new Random(240915);
        var faker = new Faker("en");
        var unitTypes = await db.UnitTypes.ToListAsync();
        var activeTypes = unitTypes.Where(t => t.IsActive).ToList();
        var inactiveType = unitTypes.First(t => !t.IsActive);

        var properties = new List<Property>();
        for (var i = 0; i < 3; i++)
        {
            properties.Add(new Property
            {
                Name = faker.Company.CompanyName() + " Residences",
                AddressLine1 = faker.Address.StreetAddress(),
                City = faker.Address.City(),
                State = faker.Address.StateAbbr(),
                PostalCode = faker.Address.ZipCode()
            });
        }
        db.Properties.AddRange(properties);
        await db.SaveChangesAsync();

        var units = new List<Unit>();
        foreach (var property in properties)
        {
            for (var n = 1; n <= 4; n++)
            {
                var type = n == 4 && property == properties[0] ? inactiveType : faker.PickRandom(activeTypes);
                units.Add(new Unit
                {
                    PropertyId = property.Id,
                    UnitNumber = $"{100 + n}",
                    Bedrooms = faker.Random.Int(0, 3),
                    MonthlyRent = faker.Finance.Amount(900, 2800, 2),
                    UnitTypeId = type.Id
                });
            }
        }
        db.Units.AddRange(units);
        await db.SaveChangesAsync();

        // One leased unit (approved path)
        var leasedUnit = units[0];
        var availableUnits = units.Skip(1).ToList();

        async Task<RentalApplication> CreateApp(ApplicationUser user, Unit unit, ApplicationStatus status, bool withSections)
        {
            var app = new RentalApplication
            {
                ApplicantUserId = user.Id,
                UnitId = unit.Id,
                Status = status,
                CurrentSection = ApplicationWizardSection.Summary,
                FullName = withSections ? user.FullName : null,
                Phone = withSections ? faker.Phone.PhoneNumber() : null,
                Email = withSections ? user.Email : null,
                CurrentAddress = withSections ? faker.Address.FullAddress() : null,
                ApplicantInfoSaved = withSections,
                ResidenceHistorySaved = withSections,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-faker.Random.Int(2, 30)),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-faker.Random.Int(0, 2))
            };
            db.RentalApplications.Add(app);
            await db.SaveChangesAsync();

            if (withSections)
            {
                db.ResidenceHistories.Add(new ResidenceHistory
                {
                    RentalApplicationId = app.Id,
                    Address = faker.Address.FullAddress(),
                    LandlordName = faker.Name.FullName(),
                    LandlordPhone = faker.Phone.PhoneNumber(),
                    MoveInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-3)),
                    MoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1))
                });
            }

            db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                RentalApplicationId = app.Id,
                FromStatus = null,
                ToStatus = ApplicationStatus.Draft,
                ChangedByUserId = user.Id,
                ChangedByDisplayName = user.FullName,
                Comment = "Seeded application",
                ChangedAtUtc = app.CreatedAtUtc
            });

            if (status != ApplicationStatus.Draft)
            {
                db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
                {
                    RentalApplicationId = app.Id,
                    FromStatus = ApplicationStatus.Draft,
                    ToStatus = status == ApplicationStatus.Returned ? ApplicationStatus.Submitted : status,
                    ChangedByUserId = user.Id,
                    ChangedByDisplayName = user.FullName,
                    Comment = "Seed transition",
                    ChangedAtUtc = app.CreatedAtUtc.AddHours(2)
                });
            }

            if (status == ApplicationStatus.Returned)
            {
                db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
                {
                    RentalApplicationId = app.Id,
                    FromStatus = ApplicationStatus.Submitted,
                    ToStatus = ApplicationStatus.Returned,
                    ChangedByUserId = managers[0].Id,
                    ChangedByDisplayName = managers[0].FullName,
                    Comment = "Please update residence history.",
                    ChangedAtUtc = app.UpdatedAtUtc
                });
                app.CurrentSection = ApplicationWizardSection.ApplicantInfo;
            }

            if (status is ApplicationStatus.Approved or ApplicationStatus.Denied)
            {
                db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
                {
                    RentalApplicationId = app.Id,
                    FromStatus = ApplicationStatus.Submitted,
                    ToStatus = status,
                    ChangedByUserId = managers[0].Id,
                    ChangedByDisplayName = managers[0].FullName,
                    Comment = status == ApplicationStatus.Approved ? "Approved in seed." : "Does not meet criteria.",
                    ChangedAtUtc = app.UpdatedAtUtc
                });
            }

            await db.SaveChangesAsync();
            return app;
        }

        var draft = await CreateApp(applicants[0], availableUnits[0], ApplicationStatus.Draft, true);
        draft.CurrentSection = ApplicationWizardSection.ApplicantInfo;
        draft.ResidenceHistorySaved = false;
        await db.SaveChangesAsync();

        await CreateApp(applicants[0], availableUnits[1], ApplicationStatus.Submitted, true);
        await CreateApp(applicants[1], availableUnits[2], ApplicationStatus.Returned, true);
        var approved = await CreateApp(applicants[1], leasedUnit, ApplicationStatus.Approved, true);
        await CreateApp(applicants[2], availableUnits[3], ApplicationStatus.Denied, true);
        await CreateApp(applicants[2], availableUnits[4], ApplicationStatus.Withdrawn, true);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (start, end) = ApplicationRules.CreateTwelveMonthLeaseTerm(today.AddMonths(-1));
        db.Leases.Add(new Lease
        {
            UnitId = leasedUnit.Id,
            RentalApplicationId = approved.Id,
            StartDate = start,
            EndDate = end
        });
        await db.SaveChangesAsync();
    }

    private static async Task<List<ApplicationUser>> EnsureUsersAsync(
        UserManager<ApplicationUser> userManager,
        string role,
        (string Email, string FullName)[] users)
    {
        var result = new List<ApplicationUser>();
        foreach (var (email, fullName) in users)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName
                };
                var create = await userManager.CreateAsync(user, DefaultPassword);
                if (!create.Succeeded)
                    throw new InvalidOperationException($"Failed to seed user {email}: {string.Join(", ", create.Errors.Select(e => e.Description))}");
            }

            if (!await userManager.IsInRoleAsync(user, role))
                await userManager.AddToRoleAsync(user, role);

            result.Add(user);
        }
        return result;
    }
}
