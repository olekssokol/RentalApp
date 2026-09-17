using RentalApp.Domain.Enums;

namespace RentalApp.Application.Applications;

public record ReviewApplicationCommand(
    int ApplicationId,
    string ManagerUserId,
    string ManagerDisplayName,
    ReviewOutcome Outcome,
    string? Comment);
