using RentalApp.Domain.Entities;

namespace RentalApp.Domain.Services;

public static class UnitAvailability
{
    public static bool HasActiveLease(IEnumerable<Lease> leases, DateOnly asOfDate) =>
        leases.Any(l => l.StartDate <= asOfDate && l.EndDate >= asOfDate);

    public static bool IsAvailable(IEnumerable<Lease> leases, DateOnly asOfDate) =>
        !HasActiveLease(leases, asOfDate);

    public static bool CanAssignUnitType(UnitType unitType, int? currentUnitTypeId)
    {
        if (unitType.IsActive)
            return true;

        // Inactive values may remain on units that already use them.
        return currentUnitTypeId.HasValue && currentUnitTypeId.Value == unitType.Id;
    }
}
