using RentalApp.Application.Common;
using RentalApp.Domain.Entities;
using RentalApp.Domain.Enums;

namespace RentalApp.Application.Applications;

public interface IRentalApplicationRepository
{
    Task<RentalApplication?> GetDetailAsync(int id, CancellationToken ct = default);
    Task<PagedResult<ApplicationListItemDto>> ListAsync(ApplicationListQuery query, CancellationToken ct = default);
    Task<RentalApplication?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<RentalApplication?> GetWithMembersAsync(int id, CancellationToken ct = default);
    Task<RentalApplication?> GetWithResidencesAsync(int id, CancellationToken ct = default);
    Task<RentalApplication?> GetWithUnitLeasesAsync(int id, CancellationToken ct = default);
    Task<Unit?> GetUnitWithLeasesAsync(int unitId, CancellationToken ct = default);
    Task AddAsync(RentalApplication application, CancellationToken ct = default);
    Task AddResidenceAsync(ResidenceHistory residence, CancellationToken ct = default);
    Task<ResidenceHistory?> GetResidenceAsync(int applicationId, int residenceId, CancellationToken ct = default);
    void RemoveResidence(ResidenceHistory residence);
    void AddApplicant(ApplicationApplicant applicant);
    void RemoveApplicant(ApplicationApplicant applicant);
    void AddLease(Lease lease);
    void AddStatusHistory(ApplicationStatusHistory history);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task<IApplicationTransaction> BeginSerializableAsync(CancellationToken ct = default);
    bool IsSerializationFailure(Exception exception);

    /// <summary>Atomic conditional update of Applicant Info. Returns false when the expected version does not match.</summary>
    Task<bool> TrySaveApplicantInfoAsync(
        int applicationId,
        int expectedVersion,
        string? fullName,
        string? phone,
        string? email,
        string? currentAddress,
        bool applicantInfoSaved,
        DateTime updatedAtUtc,
        CancellationToken ct = default);

    /// <summary>Atomic conditional bump of Residence History version. Returns false when expected version does not match.</summary>
    Task<bool> TryBumpResidenceHistoryVersionAsync(
        int applicationId,
        int expectedVersion,
        bool residenceHistorySaved,
        DateTime updatedAtUtc,
        CancellationToken ct = default);

    Task AdvanceCurrentSectionIfBehindAsync(
        int applicationId,
        ApplicationWizardSection target,
        CancellationToken ct = default);
}
