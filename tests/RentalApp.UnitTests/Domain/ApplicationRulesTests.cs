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
    public void StatusRules_ForEachStatus_EnforceAllowedActions(
        ApplicationStatus status, bool editable, bool submittable, bool withdrawable, bool reviewable, bool terminal)
    {
        Assert.Equal(editable, ApplicationRules.CanEditSections(status));
        Assert.Equal(submittable, ApplicationRules.CanSubmit(status));
        Assert.Equal(withdrawable, ApplicationRules.CanWithdraw(status));
        Assert.Equal(reviewable, ApplicationRules.CanReview(status));
        Assert.Equal(terminal, ApplicationRules.IsTerminal(status));
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
