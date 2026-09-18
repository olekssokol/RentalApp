namespace RentalApp.Application.Applications;

public record AddCoApplicantCommand(int ApplicationId, string ActorUserId, string TargetUserId);

public record RemoveCoApplicantCommand(int ApplicationId, string ActorUserId, string TargetUserId);
