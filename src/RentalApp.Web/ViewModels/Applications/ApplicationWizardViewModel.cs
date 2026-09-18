using System.ComponentModel.DataAnnotations;
using RentalApp.Application.Applications;
using RentalApp.Domain.Enums;
using RentalApp.Domain.Services;

namespace RentalApp.Web.ViewModels.Applications;

public class ApplicationWizardViewModel
{
    public int Id { get; set; }
    public ApplicationStatus Status { get; set; }
    public ApplicationWizardSection CurrentSection { get; set; }
    public ApplicationWizardSection DisplaySection { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
    public bool IsManager { get; set; }
    public bool ApplicantInfoSaved { get; set; }
    public bool ResidenceHistorySaved { get; set; }
    public bool CanClaim { get; set; }
    public bool CanRelease { get; set; }
    public bool CanReview { get; set; }
    public string? ClaimedByDisplayName { get; set; }

    [Display(Name = "Full name")]
    public string? FullName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    [Display(Name = "Current address")]
    public string? CurrentAddress { get; set; }

    public IReadOnlyList<ResidenceDto> Residences { get; set; } = [];
    public IReadOnlyList<StatusHistoryDto> StatusHistory { get; set; } = [];
    public IReadOnlyList<SubmissionBlocker> SubmissionBlockers { get; set; } = [];

    public string? Action { get; set; }

    public bool CanSubmit =>
        CanEdit
        && CurrentSection == ApplicationWizardSection.Summary
        && SubmissionBlockers.Count == 0;

    public ApplicationWizardSection? PreviousSection =>
        DisplaySection == ApplicationWizardSection.ApplicantInfo ? null : DisplaySection - 1;

    public ApplicationWizardSection? NextSection =>
        DisplaySection == ApplicationWizardSection.Summary ? null : DisplaySection + 1;

    public static ApplicationWizardViewModel From(
        ApplicationDetailDto detail,
        bool isManager,
        string currentUserId,
        ApplicationWizardSection? requestedSection = null)
    {
        var canEdit = detail.CanEdit && !isManager;
        var displaySection = ResolveDisplaySection(canEdit, detail.CurrentSection, requestedSection);
        var isClaimer = isManager
            && detail.ClaimedByUserId is not null
            && string.Equals(detail.ClaimedByUserId, currentUserId, StringComparison.Ordinal);
        return new ApplicationWizardViewModel
        {
            Id = detail.Id,
            Status = detail.Status,
            CurrentSection = detail.CurrentSection,
            DisplaySection = displaySection,
            PropertyName = detail.PropertyName,
            UnitNumber = detail.UnitNumber,
            CanEdit = canEdit,
            IsManager = isManager,
            ApplicantInfoSaved = detail.ApplicantInfoSaved,
            ResidenceHistorySaved = detail.ResidenceHistorySaved,
            CanClaim = isManager && detail.Status == ApplicationStatus.Submitted,
            CanRelease = isClaimer && detail.Status == ApplicationStatus.UnderReview,
            CanReview = isClaimer && detail.Status == ApplicationStatus.UnderReview,
            ClaimedByDisplayName = detail.ClaimedByDisplayName,
            FullName = detail.FullName,
            Phone = detail.Phone,
            Email = detail.Email,
            CurrentAddress = detail.CurrentAddress,
            Residences = detail.Residences,
            StatusHistory = detail.StatusHistory,
            SubmissionBlockers = detail.SubmissionBlockers
        };
    }

    public static ApplicationWizardSection ResolveDisplaySection(
        bool canEdit,
        ApplicationWizardSection persisted,
        ApplicationWizardSection? requested)
    {
        if (canEdit || requested is null || !Enum.IsDefined(requested.Value))
            return persisted;

        return requested.Value;
    }
}
