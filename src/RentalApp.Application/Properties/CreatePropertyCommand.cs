namespace RentalApp.Application.Properties;

public record CreatePropertyCommand(
    string Name,
    string AddressLine1,
    string City,
    string State,
    string PostalCode);
