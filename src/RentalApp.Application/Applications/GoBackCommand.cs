namespace RentalApp.Application.Applications;

public record GoBackCommand(int ApplicationId, string UserId, bool IsManager);
