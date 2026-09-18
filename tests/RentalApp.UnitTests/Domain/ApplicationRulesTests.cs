using RentalApp.Domain.Enums;
using RentalApp.Domain.Services;

namespace RentalApp.UnitTests.Domain;

public class ApplicationRulesTests
{
    [Theory]
    [InlineData(ApplicationStatus.Draft, true, true, true, false, false)]
    [InlineData(ApplicationStatus.Submitted, false, false, true, true, false)]
    [InlineData(ApplicationStatus.Returned, true, true, true, false, false)]
    [InlineData(ApplicationStatus.Approved, false, false, false, false, true)]
    [InlineData(ApplicationStatus.Denied, false, false, false, false, true)]
    [InlineData(ApplicationStatus.Withdrawn, false, false, false, false, true)]
    [InlineData(ApplicationStatus.UnderReview, false, false, false, false, false)]
    public void StatusRules_ForEachStatus_EnforceAllowedActions(
        ApplicationStatus status, bool editable, bool submittable, bool withdrawable, bool claimable, bool terminal)
    {
        Assert.Equal(editable, ApplicationRules.CanEditSections(status));
        Assert.Equal(editable, ApplicationRules.EnsureCanEdit(status).IsSuccess);
        Assert.Equal(submittable, ApplicationRules.CanSubmit(status));
        Assert.Equal(submittable, ApplicationRules.EnsureCanSubmit(status, true, true, false).IsSuccess);
        Assert.Equal(withdrawable, ApplicationRules.CanWithdraw(status));
        Assert.Equal(claimable, ApplicationRules.CanClaim(status));
        Assert.Equal(claimable, ApplicationRules.EnsureCanClaim(status).IsSuccess);
        Assert.Equal(terminal, ApplicationRules.IsTerminal(status));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void EnsureCanSubmit_SectionCompletion_RequiresBothSections(bool applicantSaved, bool residencesSaved, bool allowed)
    {
        var result = ApplicationRules.EnsureCanSubmit(ApplicationStatus.Draft, applicantSaved, residencesSaved, false);

        Assert.Equal(allowed, result.IsSuccess);
    }

    [Theory]
    [InlineData(ApplicationStatus.Draft)]
    [InlineData(ApplicationStatus.Returned)]
    public void EnsureCanSubmit_EditableStatusWithActiveLease_ReturnsFailure(ApplicationStatus status)
    {
        var result = ApplicationRules.EnsureCanSubmit(status, true, true, true);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData(ReviewOutcome.Approve, null, true)]
    [InlineData(ReviewOutcome.Approve, "", true)]
    [InlineData(ReviewOutcome.Approve, " \t\r\n", true)]
    [InlineData(ReviewOutcome.Approve, "Approved", true)]
    [InlineData(ReviewOutcome.Return, null, false)]
    [InlineData(ReviewOutcome.Return, "", false)]
    [InlineData(ReviewOutcome.Return, " \t\r\n", false)]
    [InlineData(ReviewOutcome.Return, "Please correct your address", true)]
    [InlineData(ReviewOutcome.Deny, null, false)]
    [InlineData(ReviewOutcome.Deny, "", false)]
    [InlineData(ReviewOutcome.Deny, " \t\r\n", false)]
    [InlineData(ReviewOutcome.Deny, "Does not meet requirements", true)]
    public void EnsureReviewComment_OutcomeAndComment_EnforcesRequirement(ReviewOutcome outcome, string? comment, bool allowed)
    {
        var result = ApplicationRules.EnsureReviewComment(outcome, comment);

        Assert.Equal(allowed, result.IsSuccess);
    }

    [Theory]
    [InlineData(ApplicationStatus.UnderReview, "manager", "manager", true)]
    [InlineData(ApplicationStatus.UnderReview, "manager", "other", false)]
    [InlineData(ApplicationStatus.Submitted, "manager", "manager", false)]
    [InlineData(ApplicationStatus.Draft, null, "manager", false)]
    public void EnsureCanCompleteReview_RequiresUnderReviewAndClaimer(
        ApplicationStatus status, string? claimedBy, string actor, bool allowed)
    {
        var result = ApplicationRules.EnsureCanCompleteReview(status, claimedBy, actor);

        Assert.Equal(allowed, result.IsSuccess);
    }

    [Theory]
    [InlineData(ApplicationStatus.UnderReview, "manager", "manager", true)]
    [InlineData(ApplicationStatus.UnderReview, "manager", "other", false)]
    [InlineData(ApplicationStatus.Submitted, "manager", "manager", false)]
    public void EnsureCanRelease_RequiresUnderReviewAndClaimer(
        ApplicationStatus status, string? claimedBy, string actor, bool allowed)
    {
        var result = ApplicationRules.EnsureCanRelease(status, claimedBy, actor);

        Assert.Equal(allowed, result.IsSuccess);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void EnsureUnitCanBeApproved_ActiveLease_BlocksApproval(bool activeLease, bool allowed)
    {
        var result = ApplicationRules.EnsureUnitCanBeApproved(activeLease);

        Assert.Equal(allowed, result.IsSuccess);
    }

    [Theory]
    [InlineData("2026-09-21", "2027-09-20")]
    [InlineData("2026-01-31", "2027-01-30")]
    [InlineData("2024-02-29", "2025-02-27")]
    public void CreateTwelveMonthLeaseTerm_CalendarBoundaries_UsesInclusiveEnd(string start, string expectedEnd)
    {
        var startDate = DateOnly.ParseExact(start, "yyyy-MM-dd");

        var term = ApplicationRules.CreateTwelveMonthLeaseTerm(startDate);

        Assert.Equal(startDate, term.Start);
        Assert.Equal(DateOnly.ParseExact(expectedEnd, "yyyy-MM-dd"), term.End);
    }
}
