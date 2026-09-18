using RentalApp.Domain.Enums;
using RentalApp.Domain.Services;

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
    int ApplicantInfoVersion,
    int ResidenceHistoryVersion,
    bool CanEdit,
    string? ClaimedByUserId,
    DateTime? ClaimedAtUtc,
    string? ClaimedByDisplayName,
    IReadOnlyList<ApplicationMemberDto> Members,
    IReadOnlyList<ResidenceDto> Residences,
    IReadOnlyList<StatusHistoryDto> StatusHistory,
    IReadOnlyList<SubmissionBlocker> SubmissionBlockers);
