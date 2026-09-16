namespace RentalApp.Application.Units;

public record UpdateUnitCommand(
    int Id,
    string UnitNumber,
    int Bedrooms,
    decimal MonthlyRent,
    int UnitTypeId);
