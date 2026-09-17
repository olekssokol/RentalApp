using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RentalApp.Application.Applications;
using RentalApp.Domain.Entities;
using RentalApp.Infrastructure.Persistence;

namespace RentalApp.Infrastructure.Applications;

public class RentalApplicationRepository : IRentalApplicationRepository
{
    private readonly AppDbContext _db;

    public RentalApplicationRepository(AppDbContext db) => _db = db;

    public Task<RentalApplication?> GetDetailAsync(int id, CancellationToken ct = default) =>
        _db.RentalApplications
            .AsNoTracking()
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.Residences)
            .Include(a => a.StatusHistory)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<ApplicationListItemDto>> ListAsync(ApplicationListQuery query, CancellationToken ct = default)
    {
        var applications = _db.RentalApplications
            .AsNoTracking()
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .AsQueryable();

        if (!query.IsManager)
            applications = applications.Where(a => a.ApplicantUserId == query.UserId);

        if (query.Status.HasValue)
            applications = applications.Where(a => a.Status == query.Status.Value);

        if (query.PropertyId.HasValue)
            applications = applications.Where(a => a.Unit.PropertyId == query.PropertyId.Value);

        return await applications
            .OrderByDescending(a => a.UpdatedAtUtc)
            .Select(a => new ApplicationListItemDto(
                a.Id,
                a.Status,
                a.FullName ?? "(incomplete)",
                a.Unit.Property.Name,
                a.Unit.UnitNumber,
                a.UpdatedAtUtc,
                a.Unit.PropertyId))
            .ToListAsync(ct);
    }

    public Task<RentalApplication?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.RentalApplications.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<RentalApplication?> GetWithResidencesAsync(int id, CancellationToken ct = default) =>
        _db.RentalApplications.Include(a => a.Residences).FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<RentalApplication?> GetWithUnitLeasesAsync(int id, CancellationToken ct = default) =>
        _db.RentalApplications
            .Include(a => a.Unit).ThenInclude(u => u.Leases)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Unit?> GetUnitWithLeasesAsync(int unitId, CancellationToken ct = default) =>
        _db.Units.Include(u => u.Leases).FirstOrDefaultAsync(u => u.Id == unitId, ct);

    public async Task AddAsync(RentalApplication application, CancellationToken ct = default) =>
        await _db.RentalApplications.AddAsync(application, ct);

    public async Task AddResidenceAsync(ResidenceHistory residence, CancellationToken ct = default) =>
        await _db.ResidenceHistories.AddAsync(residence, ct);

    public Task<ResidenceHistory?> GetResidenceAsync(int applicationId, int residenceId, CancellationToken ct = default) =>
        _db.ResidenceHistories.FirstOrDefaultAsync(r => r.Id == residenceId && r.RentalApplicationId == applicationId, ct);

    public void RemoveResidence(ResidenceHistory residence) => _db.ResidenceHistories.Remove(residence);

    public void AddLease(Lease lease) => _db.Leases.Add(lease);

    public void AddStatusHistory(ApplicationStatusHistory history) =>
        _db.ApplicationStatusHistories.Add(history);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task<IApplicationTransaction> BeginSerializableAsync(CancellationToken ct = default)
    {
        var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        return new EfApplicationTransaction(transaction);
    }

    public bool IsSerializationFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && sql.Number is 1205 or 3960 or 3961)
                return true;
        }

        return false;
    }

    private sealed class EfApplicationTransaction : IApplicationTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfApplicationTransaction(IDbContextTransaction transaction) => _transaction = transaction;

        public Task CommitAsync(CancellationToken ct = default) => _transaction.CommitAsync(ct);

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
