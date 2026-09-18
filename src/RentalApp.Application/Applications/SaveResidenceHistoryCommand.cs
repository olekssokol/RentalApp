namespace RentalApp.Application.Applications;

public record SaveResidenceHistoryCommand(
    int ApplicationId,
    string UserId,
    bool IsManager,
    bool Advance,
    int ExpectedResidenceHistoryVersion);
