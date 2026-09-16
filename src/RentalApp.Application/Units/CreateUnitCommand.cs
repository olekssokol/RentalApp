namespace RentalApp.Application.Units;

public record CreateUnitCommand(
    int PropertyId,
    string UnitNumber,
    int Bedrooms,
    decimal MonthlyRent,
    int UnitTypeId);
