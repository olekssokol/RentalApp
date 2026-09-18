using RentalApp.Domain.Enums;

namespace RentalApp.Application.Applications;

public record ApplicationListItemDto(
    int Id,
    ApplicationStatus Status,
    string ApplicantName,
    string PropertyName,
    string UnitNumber,
    DateTime UpdatedAtUtc,
    int PropertyId,
    string? ClaimedByUserId,
    string? ClaimedByDisplayName);
