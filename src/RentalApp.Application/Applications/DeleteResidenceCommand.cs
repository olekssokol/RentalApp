namespace RentalApp.Application.Applications;

public record DeleteResidenceCommand(
    int ApplicationId,
    int ResidenceId,
    string UserId,
    bool IsManager);
