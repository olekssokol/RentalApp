using RentalApp.Domain.Common;
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

    public static bool CanClaim(ApplicationStatus status) =>
        status == ApplicationStatus.Submitted;

    public static Result EnsureCanEdit(ApplicationStatus status)
    {
        return CanEditSections(status)
            ? Result.Success()
            : Result.Failure("This application can no longer be edited.");
    }

    public static Result EnsureCanSubmit(ApplicationStatus status, bool unitHasActiveLease)
    {
        if (!CanSubmit(status))
            return Result.Failure("This application cannot be submitted in its current status.");

        if (unitHasActiveLease)
            return Result.Failure("This unit already has an active lease and cannot accept a new application submission.");

        return Result.Success();
    }

    public static Result EnsureCanClaim(ApplicationStatus status) =>
        CanClaim(status)
            ? Result.Success()
            : Result.Failure("Only submitted applications can be claimed for review.");

    public static Result EnsureCanRelease(ApplicationStatus status, string? claimedByUserId, string actorUserId)
    {
        if (status != ApplicationStatus.UnderReview)
            return Result.Failure("Only applications under review can be released.");

        if (!string.Equals(claimedByUserId, actorUserId, StringComparison.Ordinal))
            return Result.Failure("Only the manager who claimed this application can release it.");

        return Result.Success();
    }

    public static Result EnsureCanCompleteReview(ApplicationStatus status, string? claimedByUserId, string actorUserId)
    {
        if (status != ApplicationStatus.UnderReview)
            return Result.Failure("Only claimed applications under review can be completed.");

        if (!string.Equals(claimedByUserId, actorUserId, StringComparison.Ordinal))
            return Result.Failure("Only the manager who claimed this application can complete the review.");

        return Result.Success();
    }

    public static Result EnsureReviewComment(ReviewOutcome outcome, string? comment)
    {
        if (outcome is ReviewOutcome.Return or ReviewOutcome.Deny && string.IsNullOrWhiteSpace(comment))
            return Result.Failure("A comment is required when returning or denying an application.");

        return Result.Success();
    }

    public static Result EnsureUnitCanBeApproved(bool unitHasActiveLease) =>
        unitHasActiveLease
            ? Result.Failure("This unit already has an active lease. Approval is not allowed.")
            : Result.Success();

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
