namespace RentalApp.Application.Units;

public record UnitDto(
    int Id,
    int PropertyId,
    string PropertyName,
    string UnitNumber,
    int Bedrooms,
    decimal MonthlyRent,
    int UnitTypeId,
    string UnitTypeName,
    bool IsAvailable);
