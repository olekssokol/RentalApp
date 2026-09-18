namespace RentalApp.Application.Applications;

public record SaveApplicantInfoCommand(
    int ApplicationId,
    string UserId,
    bool IsManager,
    string? FullName,
    string? Phone,
    string? Email,
    string? CurrentAddress,
    bool Advance,
    int ExpectedApplicantInfoVersion);
