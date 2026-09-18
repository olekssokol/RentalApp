namespace RentalApp.Domain.Services;

public sealed record ResidenceInput(
    string? Address,
    string? LandlordName,
    string? LandlordPhone,
    DateOnly? MoveInDate,
    DateOnly? MoveOutDate);
