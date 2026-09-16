using RentalApp.Domain.Enums;

namespace RentalApp.Domain.Services;

public static class ApplicationRules
{
    public static bool IsTerminal(ApplicationStatus status) =>
        status is ApplicationStatus.Approved or ApplicationStatus.Denied or ApplicationStatus.Withdrawn;

    public static bool CanEditSections(ApplicationStatus status) =>
        status is ApplicationStatus.Draft or ApplicationStatus.Returned;

    public static bool CanSubmit(ApplicationStatus status) =>
        status is ApplicationStatus.Draft or ApplicationStatus.Returned;

    public static bool CanWithdraw(ApplicationStatus status) =>
        status is ApplicationStatus.Draft or ApplicationStatus.Submitted or ApplicationStatus.Returned;

    public static bool CanReview(ApplicationStatus status) =>
        status == ApplicationStatus.Submitted;

    public static ApplicationStatus MapOutcomeToStatus(ReviewOutcome outcome) => outcome switch
    {
        ReviewOutcome.Approve => ApplicationStatus.Approved,
        ReviewOutcome.Return => ApplicationStatus.Returned,
        ReviewOutcome.Deny => ApplicationStatus.Denied,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };

    public static (DateOnly Start, DateOnly End) CreateTwelveMonthLeaseTerm(DateOnly startDate) =>
        (startDate, startDate.AddMonths(12).AddDays(-1));
}
