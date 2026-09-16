using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentalApp.Domain.Entities;
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

        await db.Database.EnsureCreatedAsync();
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

        await EnsureUsersAsync(userManager, AppRoles.PropertyManager,
        [
            ("pm1@rental.local", "Pat Manager"),
            ("pm2@rental.local", "Morgan Manager")
        ]);

        await EnsureUsersAsync(userManager, AppRoles.Applicant,
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
