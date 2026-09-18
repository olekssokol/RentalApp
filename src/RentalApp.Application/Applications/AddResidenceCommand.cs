namespace RentalApp.Application.Applications;

public record AddResidenceCommand(
    int ApplicationId,
    string UserId,
    bool IsManager,
    string? Address,
    string? LandlordName,
    string? LandlordPhone,
    DateOnly? MoveInDate,
    DateOnly? MoveOutDate,
    int ExpectedResidenceHistoryVersion);
