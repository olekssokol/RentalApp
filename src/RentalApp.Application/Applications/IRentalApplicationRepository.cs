using RentalApp.Domain.Entities;

namespace RentalApp.Application.Applications;

public interface IRentalApplicationRepository
{
    Task<RentalApplication?> GetDetailAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationListItemDto>> ListAsync(ApplicationListQuery query, CancellationToken ct = default);
    Task<RentalApplication?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<RentalApplication?> GetWithResidencesAsync(int id, CancellationToken ct = default);
    Task<RentalApplication?> GetWithUnitLeasesAsync(int id, CancellationToken ct = default);
    Task<Unit?> GetUnitWithLeasesAsync(int unitId, CancellationToken ct = default);
    Task AddAsync(RentalApplication application, CancellationToken ct = default);
    Task AddResidenceAsync(ResidenceHistory residence, CancellationToken ct = default);
    Task<ResidenceHistory?> GetResidenceAsync(int applicationId, int residenceId, CancellationToken ct = default);
    void RemoveResidence(ResidenceHistory residence);
    void AddLease(Lease lease);
    void AddStatusHistory(ApplicationStatusHistory history);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<IApplicationTransaction> BeginSerializableAsync(CancellationToken ct = default);
    bool IsSerializationFailure(Exception exception);
}
