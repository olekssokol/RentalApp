using System.ComponentModel.DataAnnotations;
using RentalApp.Application.Applications;
using RentalApp.Domain.Enums;

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

    [Required, MaxLength(200), Display(Name = "Full name")]
    public string? FullName { get; set; }

    [Required, MaxLength(50)]
    public string? Phone { get; set; }

    [Required, MaxLength(256), EmailAddress]
    public string? Email { get; set; }

    [Required, MaxLength(500), Display(Name = "Current address")]
    public string? CurrentAddress { get; set; }

    public IReadOnlyList<ResidenceDto> Residences { get; set; } = [];
    public IReadOnlyList<StatusHistoryDto> StatusHistory { get; set; } = [];

    public string? Action { get; set; }

    public ApplicationWizardSection? PreviousSection =>
        DisplaySection == ApplicationWizardSection.ApplicantInfo ? null : DisplaySection - 1;

    public ApplicationWizardSection? NextSection =>
        DisplaySection == ApplicationWizardSection.Summary ? null : DisplaySection + 1;

    public static ApplicationWizardViewModel From(
        ApplicationDetailDto detail,
        bool isManager,
        ApplicationWizardSection? requestedSection = null)
    {
        var canEdit = detail.CanEdit && !isManager;
        var displaySection = ResolveDisplaySection(canEdit, detail.CurrentSection, requestedSection);
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
            FullName = detail.FullName,
            Phone = detail.Phone,
            Email = detail.Email,
            CurrentAddress = detail.CurrentAddress,
            Residences = detail.Residences,
            StatusHistory = detail.StatusHistory
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
