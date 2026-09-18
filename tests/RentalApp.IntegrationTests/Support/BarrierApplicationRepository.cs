using RentalApp.Application.Applications;
using RentalApp.Application.Common;
using RentalApp.Domain.Entities;
using RentalApp.Domain.Enums;

namespace RentalApp.IntegrationTests.Support;

// The barrier is after the SERIALIZABLE read, so both operations observe the same
// pre-write state. SQL Server must then allow at most one transaction to commit.
internal sealed class BarrierApplicationRepository : IRentalApplicationRepository
{
    private readonly IRentalApplicationRepository _inner;
    private readonly Barrier _barrier;
    private readonly BarrierPoint _point;

    public BarrierApplicationRepository(
        IRentalApplicationRepository inner,
        Barrier barrier,
        BarrierPoint point = BarrierPoint.GetWithUnitLeases)
    {
        _inner = inner;
        _barrier = barrier;
        _point = point;
    }

    public Task<RentalApplication?> GetDetailAsync(int id, CancellationToken ct = default) => _inner.GetDetailAsync(id, ct);
    public Task<PagedResult<ApplicationListItemDto>> ListAsync(ApplicationListQuery query, CancellationToken ct = default) => _inner.ListAsync(query, ct);

    public async Task<RentalApplication?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var application = await _inner.GetByIdAsync(id, ct);
        if (_point == BarrierPoint.GetById)
            Wait(ct);
        return application;
    }

    public Task<RentalApplication?> GetWithMembersAsync(int id, CancellationToken ct = default) => _inner.GetWithMembersAsync(id, ct);
    public Task<RentalApplication?> GetWithResidencesAsync(int id, CancellationToken ct = default) => _inner.GetWithResidencesAsync(id, ct);

    public async Task<RentalApplication?> GetWithUnitLeasesAsync(int id, CancellationToken ct = default)
    {
        var application = await _inner.GetWithUnitLeasesAsync(id, ct);
        if (_point == BarrierPoint.GetWithUnitLeases)
            Wait(ct);
        return application;
    }

    public Task<Unit?> GetUnitWithLeasesAsync(int unitId, CancellationToken ct = default) => _inner.GetUnitWithLeasesAsync(unitId, ct);
    public Task AddAsync(RentalApplication application, CancellationToken ct = default) => _inner.AddAsync(application, ct);
    public Task AddResidenceAsync(ResidenceHistory residence, CancellationToken ct = default) => _inner.AddResidenceAsync(residence, ct);
    public Task<ResidenceHistory?> GetResidenceAsync(int applicationId, int residenceId, CancellationToken ct = default) => _inner.GetResidenceAsync(applicationId, residenceId, ct);
    public void RemoveResidence(ResidenceHistory residence) => _inner.RemoveResidence(residence);
    public void AddApplicant(ApplicationApplicant applicant) => _inner.AddApplicant(applicant);
    public void RemoveApplicant(ApplicationApplicant applicant) => _inner.RemoveApplicant(applicant);
    public void AddLease(Lease lease) => _inner.AddLease(lease);
    public void AddStatusHistory(ApplicationStatusHistory history) => _inner.AddStatusHistory(history);
    public Task SaveChangesAsync(CancellationToken ct = default) => _inner.SaveChangesAsync(ct);
    public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct = default) => _inner.BeginTransactionAsync(ct);
    public Task<IApplicationTransaction> BeginSerializableAsync(CancellationToken ct = default) => _inner.BeginSerializableAsync(ct);
    public bool IsSerializationFailure(Exception exception) => _inner.IsSerializationFailure(exception);
    public Task<bool> TrySaveApplicantInfoAsync(
        int applicationId, int expectedVersion, string? fullName, string? phone, string? email, string? currentAddress,
        bool applicantInfoSaved, DateTime updatedAtUtc, CancellationToken ct = default) =>
        _inner.TrySaveApplicantInfoAsync(applicationId, expectedVersion, fullName, phone, email, currentAddress, applicantInfoSaved, updatedAtUtc, ct);
    public Task<bool> TryBumpResidenceHistoryVersionAsync(
        int applicationId, int expectedVersion, bool residenceHistorySaved, DateTime updatedAtUtc, CancellationToken ct = default) =>
        _inner.TryBumpResidenceHistoryVersionAsync(applicationId, expectedVersion, residenceHistorySaved, updatedAtUtc, ct);
    public Task AdvanceCurrentSectionIfBehindAsync(int applicationId, ApplicationWizardSection target, CancellationToken ct = default) =>
        _inner.AdvanceCurrentSectionIfBehindAsync(applicationId, target, ct);

    private void Wait(CancellationToken ct)
    {
        if (!_barrier.SignalAndWait(TimeSpan.FromSeconds(20), ct))
            throw new TimeoutException("Competing operation did not reach the protected read.");
    }
}

internal enum BarrierPoint
{
    GetWithUnitLeases,
    GetById
}
