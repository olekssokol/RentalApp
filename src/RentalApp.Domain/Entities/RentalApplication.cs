using RentalApp.Domain.Enums;

namespace RentalApp.Domain.Entities;

public class RentalApplication : Entity
{
    /// <summary>Creator / started-by metadata only — not an ownership source. Membership is <see cref="Applicants"/>.</summary>
    public string ApplicantUserId { get; set; } = string.Empty;

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;
    public ApplicationWizardSection CurrentSection { get; set; } = ApplicationWizardSection.ApplicantInfo;

    public string? ClaimedByUserId { get; set; }
    public DateTime? ClaimedAtUtc { get; set; }

    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? CurrentAddress { get; set; }

    // Latest persisted version of that section passed ApplicationSectionRules (progress only; Submit recalculates).
    public bool ApplicantInfoSaved { get; set; }
    public bool ResidenceHistorySaved { get; set; }

    /// <summary>Optimistic concurrency token for Applicant Info only (bumped via conditional UPDATE).</summary>
    public int ApplicantInfoVersion { get; set; }

    /// <summary>Optimistic concurrency token for Residence History only (bumped via conditional UPDATE).</summary>
    public int ResidenceHistoryVersion { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ApplicationApplicant> Applicants { get; set; } = new List<ApplicationApplicant>();
    public ICollection<ResidenceHistory> Residences { get; set; } = new List<ResidenceHistory>();
    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = new List<ApplicationStatusHistory>();
    public ICollection<PropertyManagerNote> PropertyManagerNotes { get; set; } = new List<PropertyManagerNote>();
    public Lease? Lease { get; set; }
}
