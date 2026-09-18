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
    public int ApplicantInfoVersion { get; set; }
    public int ResidenceHistoryVersion { get; set; }
    public bool CanClaim { get; set; }
    public bool CanRelease { get; set; }
    public bool CanReview { get; set; }
    public string? ClaimedByDisplayName { get; set; }
    public string CreatorUserId { get; set; } = string.Empty;
    public IReadOnlyList<MemberRow> Members { get; set; } = [];

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
    public string? CoApplicantEmail { get; set; }

    public bool CanSubmit =>
        CanEdit
        && DisplaySection == ApplicationWizardSection.Summary
        && SubmissionBlockers.Count == 0;

    public bool IsMultiMember => Members.Count > 1;

    public ApplicationWizardSection? PreviousSection =>
        DisplaySection == ApplicationWizardSection.ApplicantInfo ? null : DisplaySection - 1;

    public ApplicationWizardSection? NextSection =>
        DisplaySection == ApplicationWizardSection.Summary ? null : DisplaySection + 1;

    public static ApplicationWizardViewModel From(
        ApplicationDetailDto detail,
        bool isManager,
        string currentUserId,
        ApplicationWizardSection? requestedSection = null,
        IReadOnlyDictionary<string, string>? memberNames = null)
    {
        var canEdit = detail.CanEdit && !isManager;
        var displaySection = ResolveDisplaySection(
            canEdit, detail.Members.Count, detail.CurrentSection, requestedSection);
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
            ApplicantInfoVersion = detail.ApplicantInfoVersion,
            ResidenceHistoryVersion = detail.ResidenceHistoryVersion,
            CanClaim = isManager && detail.Status == ApplicationStatus.Submitted,
            CanRelease = isClaimer && detail.Status == ApplicationStatus.UnderReview,
            CanReview = isClaimer && detail.Status == ApplicationStatus.UnderReview,
            ClaimedByDisplayName = detail.ClaimedByDisplayName,
            CreatorUserId = detail.ApplicantUserId,
            Members = detail.Members.Select(m => new MemberRow(
                m.UserId,
                memberNames != null && memberNames.TryGetValue(m.UserId, out var name) ? name : m.UserId,
                m.IsCreator)).ToList(),
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
        int memberCount,
        ApplicationWizardSection persisted,
        ApplicationWizardSection? requested)
    {
        // Read-only browse, or multi-member edit: display follows request so shared CurrentSection cannot hijack UI.
        if (requested is not null && Enum.IsDefined(requested.Value) && (!canEdit || memberCount > 1))
            return requested.Value;

        return persisted;
    }

    public sealed record MemberRow(string UserId, string DisplayName, bool IsCreator);
}
