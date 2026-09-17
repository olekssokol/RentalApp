namespace RentalApp.Application.Applications;

public record ResidenceDto(
    int Id,
    string Address,
    string LandlordName,
    string LandlordPhone,
    DateOnly MoveInDate,
    DateOnly? MoveOutDate);
