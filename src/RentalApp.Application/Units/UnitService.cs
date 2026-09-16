using RentalApp.Domain.Common;
using RentalApp.Domain.Entities;
using RentalApp.Domain.Services;

namespace RentalApp.Application.Units;

public class UnitService : IUnitService
{
    private readonly IUnitRepository _units;

    public UnitService(IUnitRepository units) => _units = units;

    public async Task<IReadOnlyList<UnitDto>> GetByPropertyAsync(int propertyId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var units = await _units.ListByPropertyAsync(propertyId, ct);
        return units.Select(u => Map(u, today)).ToList();
    }

    public async Task<IReadOnlyList<UnitDto>> GetAvailableAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var units = await _units.ListAllWithLeasesAsync(ct);
        return units.Where(u => UnitAvailability.IsAvailable(u.Leases, today)).Select(u => Map(u, today)).ToList();
    }

    public async Task<UnitDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var unit = await _units.GetByIdWithDetailsAsync(id, ct);
        return unit is null ? null : Map(unit, today);
    }

    public async Task<Result<int>> CreateAsync(CreateUnitCommand command, CancellationToken ct = default)
    {
        if (!await _units.PropertyExistsAsync(command.PropertyId, ct))
            return Result.Failure<int>("Property not found.");

        var typeCheck = await ValidateUnitTypeAsync(command.UnitTypeId, null, ct);
        if (typeCheck.IsFailure)
            return Result.Failure<int>(typeCheck.Error!);

        if (await _units.NumberExistsAsync(command.PropertyId, command.UnitNumber.Trim(), null, ct))
            return Result.Failure<int>("A unit with this number already exists for the property.");

        var entity = new Unit
        {
            PropertyId = command.PropertyId,
            UnitNumber = command.UnitNumber.Trim(),
            Bedrooms = command.Bedrooms,
            MonthlyRent = command.MonthlyRent,
            UnitTypeId = command.UnitTypeId
        };
        await _units.AddAsync(entity, ct);
        await _units.SaveChangesAsync(ct);
        return Result.Success(entity.Id);
    }

    public async Task<Result> UpdateAsync(UpdateUnitCommand command, CancellationToken ct = default)
    {
        var entity = await _units.GetTrackedAsync(command.Id, ct);
        if (entity is null)
            return Result.Failure("Unit not found.");

        var typeCheck = await ValidateUnitTypeAsync(command.UnitTypeId, entity.UnitTypeId, ct);
        if (typeCheck.IsFailure)
            return typeCheck;

        if (await _units.NumberExistsAsync(entity.PropertyId, command.UnitNumber.Trim(), command.Id, ct))
            return Result.Failure("A unit with this number already exists for the property.");

        entity.UnitNumber = command.UnitNumber.Trim();
        entity.Bedrooms = command.Bedrooms;
        entity.MonthlyRent = command.MonthlyRent;
        entity.UnitTypeId = command.UnitTypeId;
        await _units.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _units.GetWithAppsAndLeasesAsync(id, ct);
        if (entity is null)
            return Result.Failure("Unit not found.");

        if (entity.Applications.Count > 0 || entity.Leases.Count > 0)
            return Result.Failure("Cannot delete a unit that has applications or leases.");

        _units.Remove(entity);
        await _units.SaveChangesAsync(ct);
        return Result.Success();
    }

    public Task<IReadOnlyList<UnitTypeDto>> GetUnitTypesForSelectAsync(int? currentUnitTypeId = null, CancellationToken ct = default) =>
        _units.GetUnitTypesForSelectAsync(currentUnitTypeId, ct);

    private async Task<Result> ValidateUnitTypeAsync(int unitTypeId, int? currentUnitTypeId, CancellationToken ct)
    {
        var unitType = await _units.GetUnitTypeAsync(unitTypeId, ct);
        if (unitType is null)
            return Result.Failure("Unit type not found.");

        if (!UnitAvailability.CanAssignUnitType(unitType, currentUnitTypeId))
            return Result.Failure("Inactive unit types cannot be selected for other units.");

        return Result.Success();
    }

    private static UnitDto Map(Unit unit, DateOnly today) =>
        new(unit.Id, unit.PropertyId, unit.Property.Name, unit.UnitNumber, unit.Bedrooms, unit.MonthlyRent,
            unit.UnitTypeId, unit.UnitType.Name, UnitAvailability.IsAvailable(unit.Leases, today));
}
