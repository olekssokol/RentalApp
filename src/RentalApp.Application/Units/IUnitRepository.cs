using RentalApp.Domain.Entities;

namespace RentalApp.Application.Units;

public interface IUnitRepository
{
    Task<IReadOnlyList<Unit>> ListByPropertyAsync(int propertyId, CancellationToken ct = default);
    Task<IReadOnlyList<Unit>> ListAllWithLeasesAsync(CancellationToken ct = default);
    Task<Unit?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
    Task<Unit?> GetTrackedAsync(int id, CancellationToken ct = default);
    Task<Unit?> GetWithAppsAndLeasesAsync(int id, CancellationToken ct = default);
    Task<bool> PropertyExistsAsync(int propertyId, CancellationToken ct = default);
    Task<bool> NumberExistsAsync(int propertyId, string unitNumber, int? exceptUnitId, CancellationToken ct = default);
    Task<UnitType?> GetUnitTypeAsync(int unitTypeId, CancellationToken ct = default);
    Task<IReadOnlyList<UnitTypeDto>> GetUnitTypesForSelectAsync(int? currentUnitTypeId, CancellationToken ct = default);
    Task AddAsync(Unit unit, CancellationToken ct = default);
    void Remove(Unit unit);
    Task SaveChangesAsync(CancellationToken ct = default);
}
