namespace RentalApp.Application.Properties;

public record PropertyDto(
    int Id,
    string Name,
    string AddressLine1,
    string City,
    string State,
    string PostalCode,
    int UnitCount);
