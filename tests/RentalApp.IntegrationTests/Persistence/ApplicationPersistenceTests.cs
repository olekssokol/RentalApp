using Microsoft.EntityFrameworkCore;
using RentalApp.Application.Applications;
using RentalApp.Domain.Entities;
using RentalApp.Domain.Enums;
using RentalApp.Infrastructure.Applications;
using RentalApp.IntegrationTests.Support;

namespace RentalApp.IntegrationTests.Persistence;

public class ApplicationPersistenceTests
{
    [Fact]
    public async Task ListAsync_ApplicantStatusPropertyAndCombinedFilters_ReturnExpectedDatabaseRows()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        var graph = await database.AddApplicationsAsync(
            ("alice", ApplicationStatus.Draft, 1),
            ("alice", ApplicationStatus.Submitted, 1),
            ("alice", ApplicationStatus.Submitted, 2),
            ("bob", ApplicationStatus.Submitted, 1),
            ("bob", ApplicationStatus.Denied, 2));
        await using var db = database.CreateContext();
        var repository = new RentalApplicationRepository(db);

        var own = await repository.ListAsync(new("alice", false, null, null));
        var status = await repository.ListAsync(new("manager", true, ApplicationStatus.Submitted, null));
        var property = await repository.ListAsync(new("manager", true, null, graph.SecondPropertyId));
        var combined = await repository.ListAsync(new("alice", false, ApplicationStatus.Submitted, graph.FirstPropertyId));

        Assert.Equal(graph.ApplicationIds[..3].Order(), own.Select(x => x.Id).Order());
        Assert.Equal(graph.ApplicationIds[1..4].Order(), status.Select(x => x.Id).Order());
        Assert.Equal(new[] { graph.ApplicationIds[2], graph.ApplicationIds[4] }.Order(), property.Select(x => x.Id).Order());
        Assert.Equal(new[] { graph.ApplicationIds[1] }, combined.Select(x => x.Id));
    }

    [Fact]
    public async Task ApproveAsync_SubmittedApplication_PersistsStatusLeaseAndHistoryTogether()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        var graph = await database.AddApplicationsAsync(("alice", ApplicationStatus.Submitted, 1));
        await using (var db = database.CreateContext())
        {
            var service = new ApplicationCommandService(new RentalApplicationRepository(db));
            var result = await service.ReviewAsync(new(graph.ApplicationIds[0], "manager", "Pat Manager", ReviewOutcome.Approve, null));
            Assert.True(result.IsSuccess, result.Error);
        }

        await using var verification = database.CreateContext();
        var application = await verification.RentalApplications.SingleAsync(a => a.Id == graph.ApplicationIds[0]);
        var lease = await verification.Leases.SingleAsync(l => l.RentalApplicationId == application.Id);
        var history = await verification.ApplicationStatusHistories.SingleAsync(h => h.RentalApplicationId == application.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(ApplicationStatus.Approved, application.Status);
        Assert.Equal(graph.FirstUnitId, lease.UnitId);
        Assert.Equal(today, lease.StartDate);
        Assert.Equal(today.AddMonths(12).AddDays(-1), lease.EndDate);
        Assert.Equal(ApplicationStatus.Submitted, history.FromStatus);
        Assert.Equal(ApplicationStatus.Approved, history.ToStatus);
        Assert.Equal("manager", history.ChangedByUserId);
        Assert.Equal("Pat Manager", history.ChangedByDisplayName);
    }

    [Fact]
    public async Task ApproveAsync_UnitHasActiveLease_LeavesSubmittedApplicationAndSingleLease()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        var graph = await database.AddApplicationsAsync(
            ("lease-owner", ApplicationStatus.Approved, 1),
            ("candidate", ApplicationStatus.Submitted, 1));
        await AddActiveLeaseAsync(database, graph.FirstUnitId, graph.ApplicationIds[0]);
        await using (var db = database.CreateContext())
        {
            var service = new ApplicationCommandService(new RentalApplicationRepository(db));
            var result = await service.ReviewAsync(new(graph.ApplicationIds[1], "manager", "Pat Manager", ReviewOutcome.Approve, null));
            Assert.True(result.IsFailure);
        }

        await using var verification = database.CreateContext();
        Assert.Equal(ApplicationStatus.Submitted, await verification.RentalApplications.Where(a => a.Id == graph.ApplicationIds[1]).Select(a => a.Status).SingleAsync());
        Assert.Equal(1, await verification.Leases.CountAsync(l => l.UnitId == graph.FirstUnitId));
        Assert.False(await verification.ApplicationStatusHistories.AnyAsync(h => h.RentalApplicationId == graph.ApplicationIds[1]));
    }

    [Fact]
    public async Task SubmitAsync_UnitHasActiveLease_DoesNotSubmitOrModifyOtherOpenApplications()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        var graph = await database.AddApplicationsAsync(
            ("lease-owner", ApplicationStatus.Approved, 1),
            ("alice", ApplicationStatus.Draft, 1),
            ("bob", ApplicationStatus.Returned, 1));
        await AddActiveLeaseAsync(database, graph.FirstUnitId, graph.ApplicationIds[0]);
        await using (var db = database.CreateContext())
        {
            var service = new ApplicationCommandService(new RentalApplicationRepository(db));
            var result = await service.SubmitAsync(new(graph.ApplicationIds[1], "alice"));
            Assert.True(result.IsFailure);
        }

        await using var verification = database.CreateContext();
        var statuses = await verification.RentalApplications
            .Where(a => graph.ApplicationIds.Contains(a.Id))
            .OrderBy(a => a.Id).Select(a => a.Status).ToListAsync();
        Assert.Equal(new[] { ApplicationStatus.Approved, ApplicationStatus.Draft, ApplicationStatus.Returned }, statuses);
        Assert.Equal(1, await verification.Leases.CountAsync(l => l.UnitId == graph.FirstUnitId));
        Assert.False(await verification.ApplicationStatusHistories.AnyAsync(h => h.RentalApplicationId == graph.ApplicationIds[1]));
    }

    [Fact]
    public async Task ApproveAsync_CompetingSerializableTransactions_CreateExactlyOneLease()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        var graph = await database.AddApplicationsAsync(
            ("alice", ApplicationStatus.Submitted, 1),
            ("bob", ApplicationStatus.Submitted, 1));
        using var barrier = new Barrier(2);
        await using var firstDb = database.CreateContext();
        await using var secondDb = database.CreateContext();
        var first = new ApplicationCommandService(new BarrierApplicationRepository(new RentalApplicationRepository(firstDb), barrier));
        var second = new ApplicationCommandService(new BarrierApplicationRepository(new RentalApplicationRepository(secondDb), barrier));

        var results = await Task.WhenAll(
            Task.Run(() => first.ReviewAsync(new(graph.ApplicationIds[0], "manager-1", "Manager One", ReviewOutcome.Approve, null))),
            Task.Run(() => second.ReviewAsync(new(graph.ApplicationIds[1], "manager-2", "Manager Two", ReviewOutcome.Approve, null))));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure);
        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.Leases.CountAsync(l => l.UnitId == graph.FirstUnitId));
        var statuses = await verification.RentalApplications
            .Where(a => graph.ApplicationIds.Contains(a.Id)).Select(a => a.Status).ToListAsync();
        Assert.Equal(1, statuses.Count(s => s == ApplicationStatus.Approved));
        Assert.Equal(1, statuses.Count(s => s == ApplicationStatus.Submitted));
        Assert.Equal(1, await verification.ApplicationStatusHistories.CountAsync(h =>
            graph.ApplicationIds.Contains(h.RentalApplicationId) && h.ToStatus == ApplicationStatus.Approved));
    }

    private static async Task AddActiveLeaseAsync(SqlTestDatabase database, int unitId, int applicationId)
    {
        await using var db = database.CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Leases.Add(new Lease
        {
            UnitId = unitId,
            RentalApplicationId = applicationId,
            StartDate = today.AddDays(-1),
            EndDate = today.AddMonths(1)
        });
        await db.SaveChangesAsync();
    }
}
