using RentalApp.Domain.Common;

namespace RentalApp.Application.Units;

public interface IUnitService
{
    Task<IReadOnlyList<UnitDto>> GetByPropertyAsync(int propertyId, CancellationToken ct = default);
    Task<IReadOnlyList<UnitDto>> GetAvailableAsync(CancellationToken ct = default);
    Task<UnitDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(CreateUnitCommand command, CancellationToken ct = default);
    Task<Result> UpdateAsync(UpdateUnitCommand command, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<UnitTypeDto>> GetUnitTypesForSelectAsync(int? currentUnitTypeId = null, CancellationToken ct = default);
}
