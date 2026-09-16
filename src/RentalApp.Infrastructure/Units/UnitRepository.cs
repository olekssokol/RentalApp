using Microsoft.EntityFrameworkCore;
using RentalApp.Application.Units;
using RentalApp.Domain.Entities;
using RentalApp.Infrastructure.Persistence;

namespace RentalApp.Infrastructure.Units;

public class UnitRepository : IUnitRepository
{
    private readonly AppDbContext _db;

    public UnitRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Unit>> ListByPropertyAsync(int propertyId, CancellationToken ct = default) =>
        await QueryWithDetails()
            .Where(u => u.PropertyId == propertyId)
            .OrderBy(u => u.UnitNumber)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Unit>> ListAllWithLeasesAsync(CancellationToken ct = default) =>
        await QueryWithDetails()
            .OrderBy(u => u.Property.Name)
            .ThenBy(u => u.UnitNumber)
            .ToListAsync(ct);

    public Task<Unit?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default) =>
        QueryWithDetails().FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<Unit?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        _db.Units.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<Unit?> GetWithAppsAndLeasesAsync(int id, CancellationToken ct = default) =>
        _db.Units
            .Include(u => u.Applications)
            .Include(u => u.Leases)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> PropertyExistsAsync(int propertyId, CancellationToken ct = default) =>
        _db.Properties.AnyAsync(p => p.Id == propertyId, ct);

    public Task<bool> NumberExistsAsync(int propertyId, string unitNumber, int? exceptUnitId, CancellationToken ct = default)
    {
        var query = _db.Units.Where(u => u.PropertyId == propertyId && u.UnitNumber == unitNumber);
        if (exceptUnitId.HasValue)
            query = query.Where(u => u.Id != exceptUnitId.Value);

        return query.AnyAsync(ct);
    }

    public Task<UnitType?> GetUnitTypeAsync(int unitTypeId, CancellationToken ct = default) =>
        _db.UnitTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == unitTypeId, ct);

    public async Task<IReadOnlyList<UnitTypeDto>> GetUnitTypesForSelectAsync(int? currentUnitTypeId, CancellationToken ct = default) =>
        await _db.UnitTypes
            .AsNoTracking()
            .Where(t => t.IsActive || (currentUnitTypeId.HasValue && t.Id == currentUnitTypeId.Value))
            .OrderBy(t => t.Name)
            .Select(t => new UnitTypeDto(t.Id, t.Name, t.IsActive))
            .ToListAsync(ct);

    public async Task AddAsync(Unit unit, CancellationToken ct = default) =>
        await _db.Units.AddAsync(unit, ct);

    public void Remove(Unit unit) => _db.Units.Remove(unit);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    private IQueryable<Unit> QueryWithDetails() =>
        _db.Units
            .AsNoTracking()
            .Include(u => u.Property)
            .Include(u => u.UnitType)
            .Include(u => u.Leases);
}
