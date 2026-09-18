namespace RentalApp.Application.Applications;

public record ReleaseApplicationCommand(int ApplicationId, string ManagerUserId, string ManagerDisplayName);
