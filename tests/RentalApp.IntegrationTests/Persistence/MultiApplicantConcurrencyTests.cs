using Microsoft.EntityFrameworkCore;
using RentalApp.Application.Applications;
using RentalApp.Domain.Entities;
using RentalApp.Domain.Enums;
using RentalApp.Domain.Services;
using RentalApp.Infrastructure.Applications;
using RentalApp.IntegrationTests.Support;

namespace RentalApp.IntegrationTests.Persistence;

public class MultiApplicantConcurrencyTests
{
    [Fact]
    public async Task ConcurrentSaves_DifferentSections_BothSucceed()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var graph = await SeedMultiApplicantAsync(db);
        await using var contextA = db.CreateContext();
        await using var contextB = db.CreateContext();
        var serviceA = new ApplicationCommandService(new RentalApplicationRepository(contextA));
        var serviceB = new ApplicationCommandService(new RentalApplicationRepository(contextB));

        var applicantTask = serviceA.SaveApplicantInfoAsync(new SaveApplicantInfoCommand(
            graph.ApplicationId, graph.UserA, false,
            "Concurrent A", "555-0001", "a@example.test", "Address A",
            Advance: false, ExpectedApplicantInfoVersion: 0));
        var residenceTask = serviceB.AddResidenceAsync(new AddResidenceCommand(
            graph.ApplicationId, graph.UserB, false,
            "Prior address", "Landlord", "555-0002", new DateOnly(2020, 1, 1), null,
            ExpectedResidenceHistoryVersion: 0));

        await Task.WhenAll(applicantTask, residenceTask);
        var applicantResult = await applicantTask;
        var residenceResult = await residenceTask;

        Assert.True(applicantResult.IsSuccess);
        Assert.True(applicantResult.Value!.IsValid);
        Assert.True(residenceResult.IsSuccess);

        await using var verify = db.CreateContext();
        var app = await verify.RentalApplications.Include(a => a.Residences).SingleAsync(a => a.Id == graph.ApplicationId);
        Assert.Equal("Concurrent A", app.FullName);
        Assert.Equal(1, app.ApplicantInfoVersion);
        Assert.Equal(1, app.ResidenceHistoryVersion);
        Assert.Contains(app.Residences, r => r.Address == "Prior address");
    }

    [Fact]
    public async Task ConcurrentSaves_SameSection_SecondIsRejectedAsStale()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var graph = await SeedMultiApplicantAsync(db);
        await using var contextA = db.CreateContext();
        await using var contextB = db.CreateContext();
        var serviceA = new ApplicationCommandService(new RentalApplicationRepository(contextA));
        var serviceB = new ApplicationCommandService(new RentalApplicationRepository(contextB));

        var first = await serviceA.SaveApplicantInfoAsync(new SaveApplicantInfoCommand(
            graph.ApplicationId, graph.UserA, false,
            "First writer", "555-0001", "a@example.test", "Address A",
            Advance: false, ExpectedApplicantInfoVersion: 0));
        Assert.True(first.IsSuccess);

        var second = await serviceB.SaveApplicantInfoAsync(new SaveApplicantInfoCommand(
            graph.ApplicationId, graph.UserB, false,
            "Second writer", "555-9999", "b@example.test", "Address B",
            Advance: false, ExpectedApplicantInfoVersion: 0));

        Assert.True(second.IsFailure);
        Assert.Equal(ApplicationMembership.StaleSectionMessage, second.Error);

        await using var verify = db.CreateContext();
        var app = await verify.RentalApplications.SingleAsync(a => a.Id == graph.ApplicationId);
        Assert.Equal("First writer", app.FullName);
        Assert.Equal("555-0001", app.Phone);
        Assert.Equal(1, app.ApplicantInfoVersion);
    }

    [Fact]
    public async Task ResidenceModal_StaleVersion_Rejected()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var graph = await SeedMultiApplicantAsync(db);
        await using var contextA = db.CreateContext();
        await using var contextB = db.CreateContext();
        var serviceA = new ApplicationCommandService(new RentalApplicationRepository(contextA));
        var serviceB = new ApplicationCommandService(new RentalApplicationRepository(contextB));

        var first = await serviceA.AddResidenceAsync(new AddResidenceCommand(
            graph.ApplicationId, graph.UserA, false,
            "First residence", "Landlord", "555-0002", new DateOnly(2020, 1, 1), null,
            ExpectedResidenceHistoryVersion: 0));
        Assert.True(first.IsSuccess);

        var stale = await serviceB.AddResidenceAsync(new AddResidenceCommand(
            graph.ApplicationId, graph.UserB, false,
            "Stale residence", "Other", "555-0003", new DateOnly(2021, 1, 1), null,
            ExpectedResidenceHistoryVersion: 0));

        Assert.True(stale.IsFailure);
        Assert.Equal(ApplicationMembership.StaleSectionMessage, stale.Error);

        await using var verify = db.CreateContext();
        var app = await verify.RentalApplications.Include(a => a.Residences).SingleAsync(a => a.Id == graph.ApplicationId);
        Assert.Single(app.Residences);
        Assert.Equal("First residence", app.Residences.Single().Address);
        Assert.Equal(1, app.ResidenceHistoryVersion);
    }

    [Fact]
    public async Task CoApplicant_CanListAndEdit_ForeignCannot()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var graph = await SeedMultiApplicantAsync(db);
        await using var context = db.CreateContext();
        var repo = new RentalApplicationRepository(context);
        var queries = new ApplicationQueryService(repo);
        var commands = new ApplicationCommandService(repo);

        var detailB = await queries.GetAsync(graph.ApplicationId, graph.UserB, false);
        Assert.NotNull(detailB);

        var listB = await queries.ListAsync(new ApplicationListQuery(graph.UserB, false, null, null, 1, 10));
        Assert.Contains(listB.Items, i => i.Id == graph.ApplicationId);

        var foreign = await queries.GetAsync(graph.ApplicationId, "stranger", false);
        Assert.Null(foreign);

        var denied = await commands.SaveApplicantInfoAsync(new SaveApplicantInfoCommand(
            graph.ApplicationId, "stranger", false,
            "Nope", "555", "x@y.z", "Addr", false, 0));
        Assert.True(denied.IsFailure);
    }

    private static async Task<MultiGraph> SeedMultiApplicantAsync(SqlTestDatabase database)
    {
        await using var db = database.CreateContext();
        var type = new UnitType { Name = "Apartment", IsActive = true };
        var property = new Property
        {
            Name = "Harbor", AddressLine1 = "1 Main", City = "City", State = "ST", PostalCode = "10001"
        };
        db.AddRange(type, property);
        await db.SaveChangesAsync();
        var unit = new Unit
        {
            PropertyId = property.Id, UnitTypeId = type.Id, UnitNumber = "101", Bedrooms = 1, MonthlyRent = 1200
        };
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        const string userA = "user-a";
        const string userB = "user-b";
        var app = new RentalApplication
        {
            ApplicantUserId = userA,
            UnitId = unit.Id,
            Status = ApplicationStatus.Draft,
            CurrentSection = ApplicationWizardSection.ApplicantInfo,
            FullName = "Seed Name",
            Phone = "555-0100",
            Email = "seed@example.test",
            CurrentAddress = "Seed address",
            ApplicantInfoSaved = true,
            ResidenceHistorySaved = false
        };
        app.Applicants.Add(new ApplicationApplicant { UserId = userA, AddedAtUtc = DateTime.UtcNow });
        app.Applicants.Add(new ApplicationApplicant { UserId = userB, AddedAtUtc = DateTime.UtcNow });
        db.RentalApplications.Add(app);
        await db.SaveChangesAsync();
        return new MultiGraph(app.Id, userA, userB);
    }

    private sealed record MultiGraph(int ApplicationId, string UserA, string UserB);
}
