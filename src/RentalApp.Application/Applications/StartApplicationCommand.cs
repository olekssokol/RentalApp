namespace RentalApp.Application.Applications;

public record StartApplicationCommand(string ApplicantUserId, int UnitId, string ApplicantDisplayName);
