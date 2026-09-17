using RentalApp.Domain.Enums;

namespace RentalApp.Application.Applications;

public record ApplicationDetailDto(
    int Id,
    ApplicationStatus Status,
    ApplicationWizardSection CurrentSection,
    string ApplicantUserId,
    int UnitId,
    string PropertyName,
    string UnitNumber,
    string? FullName,
    string? Phone,
    string? Email,
    string? CurrentAddress,
    bool ApplicantInfoSaved,
    bool ResidenceHistorySaved,
    bool CanEdit,
    IReadOnlyList<ResidenceDto> Residences,
    IReadOnlyList<StatusHistoryDto> StatusHistory);
