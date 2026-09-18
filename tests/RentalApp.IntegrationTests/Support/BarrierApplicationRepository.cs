using RentalApp.Application.Applications;
using RentalApp.Application.Common;
using RentalApp.Domain.Entities;

namespace RentalApp.IntegrationTests.Support;

// The barrier is after the SERIALIZABLE read, so both approvals observe the same
// pre-insert state. SQL Server must then allow at most one transaction to commit.
internal sealed class BarrierApplicationRepository : IRentalApplicationRepository
{
    private readonly IRentalApplicationRepository _inner;
    private readonly Barrier _barrier;

    public BarrierApplicationRepository(IRentalApplicationRepository inner, Barrier barrier)
    {
        _inner = inner;
        _barrier = barrier;
    }

    public Task<RentalApplication?> GetDetailAsync(int id, CancellationToken ct = default) => _inner.GetDetailAsync(id, ct);
    public Task<PagedResult<ApplicationListItemDto>> ListAsync(ApplicationListQuery query, CancellationToken ct = default) => _inner.ListAsync(query, ct);
    public Task<RentalApplication?> GetByIdAsync(int id, CancellationToken ct = default) => _inner.GetByIdAsync(id, ct);
    public Task<RentalApplication?> GetWithResidencesAsync(int id, CancellationToken ct = default) => _inner.GetWithResidencesAsync(id, ct);
    public async Task<RentalApplication?> GetWithUnitLeasesAsync(int id, CancellationToken ct = default)
    {
        var application = await _inner.GetWithUnitLeasesAsync(id, ct);
        if (!_barrier.SignalAndWait(TimeSpan.FromSeconds(20), ct))
            throw new TimeoutException("Competing approval did not reach the protected read.");
        return application;
    }
    public Task<Unit?> GetUnitWithLeasesAsync(int unitId, CancellationToken ct = default) => _inner.GetUnitWithLeasesAsync(unitId, ct);
    public Task AddAsync(RentalApplication application, CancellationToken ct = default) => _inner.AddAsync(application, ct);
    public Task AddResidenceAsync(ResidenceHistory residence, CancellationToken ct = default) => _inner.AddResidenceAsync(residence, ct);
    public Task<ResidenceHistory?> GetResidenceAsync(int applicationId, int residenceId, CancellationToken ct = default) => _inner.GetResidenceAsync(applicationId, residenceId, ct);
    public void RemoveResidence(ResidenceHistory residence) => _inner.RemoveResidence(residence);
    public void AddLease(Lease lease) => _inner.AddLease(lease);
    public void AddStatusHistory(ApplicationStatusHistory history) => _inner.AddStatusHistory(history);
    public Task SaveChangesAsync(CancellationToken ct = default) => _inner.SaveChangesAsync(ct);
    public Task<IApplicationTransaction> BeginSerializableAsync(CancellationToken ct = default) => _inner.BeginSerializableAsync(ct);
    public bool IsSerializationFailure(Exception exception) => _inner.IsSerializationFailure(exception);
}
