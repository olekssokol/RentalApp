namespace RentalApp.Application.Properties;

public record UpdatePropertyCommand(
    int Id,
    string Name,
    string AddressLine1,
    string City,
    string State,
    string PostalCode);
