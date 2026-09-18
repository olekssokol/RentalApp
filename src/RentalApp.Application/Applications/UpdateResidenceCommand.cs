namespace RentalApp.Application.Applications;

public record UpdateResidenceCommand(
    int ApplicationId,
    int ResidenceId,
    string UserId,
    bool IsManager,
    string? Address,
    string? LandlordName,
    string? LandlordPhone,
    DateOnly? MoveInDate,
    DateOnly? MoveOutDate,
    int ExpectedResidenceHistoryVersion);
