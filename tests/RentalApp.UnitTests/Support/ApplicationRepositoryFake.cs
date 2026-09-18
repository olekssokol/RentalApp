using RentalApp.Application.Applications;
using RentalApp.Application.Common;
using RentalApp.Domain.Entities;
using RentalApp.Domain.Enums;

namespace RentalApp.UnitTests.Support;

// SQL transactions and query filtering are tested against the real repository instead.
internal sealed class ApplicationRepositoryFake : IRentalApplicationRepository
{
    public RentalApplication Application { get; } = new()
    {
        Id = 1, ApplicantUserId = "owner", UnitId = 1,
        Unit = new Unit { Id = 1, UnitNumber = "101", Property = new Property { Name = "Test property" } },
        FullName = "Original applicant", Phone = "555-0100", Email = "owner@example.test", CurrentAddress = "Original address",
        ApplicantInfoSaved = true, ResidenceHistorySaved = true, CurrentSection = ApplicationWizardSection.Summary,
        UpdatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };
    public RentalApplication? AddedApplication { get; private set; }
    public List<ApplicationStatusHistory> History { get; } = [];
    public int SaveCount { get; private set; }

    public Task<RentalApplication?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(id == Application.Id ? Application : null);
    public Task<RentalApplication?> GetDetailAsync(int id, CancellationToken ct = default) => GetByIdAsync(id, ct);
    public Task<RentalApplication?> GetWithResidencesAsync(int id, CancellationToken ct = default) => GetByIdAsync(id, ct);
    public Task<RentalApplication?> GetWithUnitLeasesAsync(int id, CancellationToken ct = default) => GetByIdAsync(id, ct);
    public Task<Unit?> GetUnitWithLeasesAsync(int unitId, CancellationToken ct = default) => Task.FromResult<Unit?>(Application.Unit);
    public Task AddAsync(RentalApplication application, CancellationToken ct = default)
    {
        application.Id = 2;
        AddedApplication = application;
        return Task.CompletedTask;
    }
    public Task AddResidenceAsync(ResidenceHistory residence, CancellationToken ct = default)
    {
        residence.Id = Application.Residences.Count + 1;
        Application.Residences.Add(residence);
        return Task.CompletedTask;
    }
    public Task<ResidenceHistory?> GetResidenceAsync(int applicationId, int residenceId, CancellationToken ct = default) =>
        Task.FromResult(Application.Residences.SingleOrDefault(r => r.RentalApplicationId == applicationId && r.Id == residenceId));
    public void RemoveResidence(ResidenceHistory residence) => Application.Residences.Remove(residence);
    public void AddStatusHistory(ApplicationStatusHistory history) => History.Add(history);
    public Task SaveChangesAsync(CancellationToken ct = default) { SaveCount++; return Task.CompletedTask; }

    public Task<PagedResult<ApplicationListItemDto>> ListAsync(ApplicationListQuery query, CancellationToken ct = default) => throw new NotSupportedException();
    public void AddLease(Lease lease) => throw new NotSupportedException();
    public Task<IApplicationTransaction> BeginSerializableAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public bool IsSerializationFailure(Exception exception) => false;
}
