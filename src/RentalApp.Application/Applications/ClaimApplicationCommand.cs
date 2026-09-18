namespace RentalApp.Application.Applications;

public record ClaimApplicationCommand(int ApplicationId, string ManagerUserId, string ManagerDisplayName);
