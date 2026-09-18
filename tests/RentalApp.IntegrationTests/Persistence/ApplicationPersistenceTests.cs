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

        Assert.Equal(3, own.FilteredTotal);
        Assert.Equal(graph.ApplicationIds[..3].Order(), own.Items.Select(x => x.Id).Order());
        Assert.Equal(3, status.FilteredTotal);
        Assert.Equal(graph.ApplicationIds[1..4].Order(), status.Items.Select(x => x.Id).Order());
        Assert.Equal(2, property.FilteredTotal);
        Assert.Equal(new[] { graph.ApplicationIds[2], graph.ApplicationIds[4] }.Order(), property.Items.Select(x => x.Id).Order());
        Assert.Equal(1, combined.FilteredTotal);
        Assert.Equal(new[] { graph.ApplicationIds[1] }, combined.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task ListAsync_SortAndPage_AreAppliedBeforeMaterializationAndKeepFilteredTotal()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        var specs = Enumerable.Range(0, 12)
            .Select(index => ($"user-{index:00}", ApplicationStatus.Draft, index % 2 + 1))
            .ToArray();
        await database.AddApplicationsAsync(specs);
        await using var db = database.CreateContext();
        var repository = new RentalApplicationRepository(db);

        var result = await repository.ListAsync(new ApplicationListQuery(
            "manager", true, null, null, Page: 2, PageSize: 10,
            Sort: ApplicationSortField.Applicant, Direction: ApplicationSortDirection.Ascending));

        Assert.Equal(12, result.FilteredTotal);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(new[] { "Applicant 8", "Applicant 9" }, result.Items.Select(item => item.ApplicantName));
    }

    [Fact]
    public async Task PropertyManagerNotes_ArePersistedAndScopedToTheirApplication()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        var graph = await database.AddApplicationsAsync(
            ("alice", ApplicationStatus.Submitted, 1),
            ("bob", ApplicationStatus.Submitted, 2));
        await using (var db = database.CreateContext())
        {
            var service = new PropertyManagerNoteService(new PropertyManagerNoteRepository(db));
            var added = await service.AddAsync(new AddPropertyManagerNoteCommand(
                graph.ApplicationIds[0], "manager", "Pat Manager", "  Private review note  ", IsManager: true));
            Assert.True(added.IsSuccess, added.Error);
        }

        await using var verification = database.CreateContext();
        var repository = new PropertyManagerNoteRepository(verification);
        var first = await repository.ListAsync(graph.ApplicationIds[0]);
        var second = await repository.ListAsync(graph.ApplicationIds[1]);

        var note = Assert.Single(first);
        Assert.Equal("Private review note", note.Text);
        Assert.Equal("Pat Manager", note.AuthorDisplayName);
        Assert.Empty(second);
    }

    [Fact]
    public async Task ApproveAsync_ClaimedApplication_PersistsStatusLeaseAndHistoryTogether()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        var graph = await database.AddApplicationsAsync(("alice", ApplicationStatus.Submitted, 1));
        await using (var db = database.CreateContext())
        {
            var service = new ApplicationCommandService(new RentalApplicationRepository(db));
            Assert.True((await service.ClaimAsync(new(graph.ApplicationIds[0], "manager", "Pat Manager"))).IsSuccess);
            var result = await service.ReviewAsync(new(graph.ApplicationIds[0], "manager", "Pat Manager", ReviewOutcome.Approve, null));
            Assert.True(result.IsSuccess, result.Error);
        }

        await using var verification = database.CreateContext();
        var application = await verification.RentalApplications.SingleAsync(a => a.Id == graph.ApplicationIds[0]);
        var lease = await verification.Leases.SingleAsync(l => l.RentalApplicationId == application.Id);
        var history = await verification.ApplicationStatusHistories
            .Where(h => h.RentalApplicationId == application.Id && h.ToStatus == ApplicationStatus.Approved)
            .SingleAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(ApplicationStatus.Approved, application.Status);
        Assert.Null(application.ClaimedByUserId);
        Assert.Null(application.ClaimedAtUtc);
        Assert.Equal(graph.FirstUnitId, lease.UnitId);
        Assert.Equal(today, lease.StartDate);
        Assert.Equal(today.AddMonths(12).AddDays(-1), lease.EndDate);
        Assert.Equal(ApplicationStatus.UnderReview, history.FromStatus);
        Assert.Equal(ApplicationStatus.Approved, history.ToStatus);
        Assert.Equal("manager", history.ChangedByUserId);
        Assert.Equal("Pat Manager", history.ChangedByDisplayName);
    }

    [Fact]
    public async Task ApproveAsync_UnitHasActiveLease_LeavesUnderReviewApplicationAndSingleLease()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        var graph = await database.AddApplicationsAsync(
            ("lease-owner", ApplicationStatus.Approved, 1),
            ("candidate", ApplicationStatus.Submitted, 1));
        await AddActiveLeaseAsync(database, graph.FirstUnitId, graph.ApplicationIds[0]);
        await using (var db = database.CreateContext())
        {
            var service = new ApplicationCommandService(new RentalApplicationRepository(db));
            Assert.True((await service.ClaimAsync(new(graph.ApplicationIds[1], "manager", "Pat Manager"))).IsSuccess);
            var result = await service.ReviewAsync(new(graph.ApplicationIds[1], "manager", "Pat Manager", ReviewOutcome.Approve, null));
            Assert.True(result.IsFailure);
        }

        await using var verification = database.CreateContext();
        var candidate = await verification.RentalApplications.SingleAsync(a => a.Id == graph.ApplicationIds[1]);
        Assert.Equal(ApplicationStatus.UnderReview, candidate.Status);
        Assert.Equal("manager", candidate.ClaimedByUserId);
        Assert.Equal(1, await verification.Leases.CountAsync(l => l.UnitId == graph.FirstUnitId));
        Assert.False(await verification.ApplicationStatusHistories.AnyAsync(h =>
            h.RentalApplicationId == graph.ApplicationIds[1] && h.ToStatus == ApplicationStatus.Approved));
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
        await using (var setup = database.CreateContext())
        {
            var service = new ApplicationCommandService(new RentalApplicationRepository(setup));
            Assert.True((await service.ClaimAsync(new(graph.ApplicationIds[0], "manager-1", "Manager One"))).IsSuccess);
            Assert.True((await service.ClaimAsync(new(graph.ApplicationIds[1], "manager-2", "Manager Two"))).IsSuccess);
        }

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
        Assert.Equal(1, statuses.Count(s => s == ApplicationStatus.UnderReview));
        Assert.Equal(1, await verification.ApplicationStatusHistories.CountAsync(h =>
            graph.ApplicationIds.Contains(h.RentalApplicationId) && h.ToStatus == ApplicationStatus.Approved));
    }

    [Fact]
    public async Task TwoManagers_ClaimSameSubmittedApplication_OnlyOneSucceeds()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        var graph = await database.AddApplicationsAsync(("alice", ApplicationStatus.Submitted, 1));
        using var barrier = new Barrier(2);
        await using var firstDb = database.CreateContext();
        await using var secondDb = database.CreateContext();
        var first = new ApplicationCommandService(
            new BarrierApplicationRepository(new RentalApplicationRepository(firstDb), barrier, BarrierPoint.GetById));
        var second = new ApplicationCommandService(
            new BarrierApplicationRepository(new RentalApplicationRepository(secondDb), barrier, BarrierPoint.GetById));

        var results = await Task.WhenAll(
            Task.Run(() => first.ClaimAsync(new(graph.ApplicationIds[0], "manager-1", "Manager One"))),
            Task.Run(() => second.ClaimAsync(new(graph.ApplicationIds[0], "manager-2", "Manager Two"))));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure);
        await using var verification = database.CreateContext();
        var application = await verification.RentalApplications.SingleAsync(a => a.Id == graph.ApplicationIds[0]);
        Assert.Equal(ApplicationStatus.UnderReview, application.Status);
        Assert.NotNull(application.ClaimedByUserId);
        Assert.Contains(application.ClaimedByUserId, new[] { "manager-1", "manager-2" });
        Assert.Equal(1, await verification.ApplicationStatusHistories.CountAsync(h =>
            h.RentalApplicationId == application.Id && h.ToStatus == ApplicationStatus.UnderReview));
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
