using RentalApp.Domain.Enums;
using RentalApp.Domain.Services;

namespace RentalApp.UnitTests.Domain;

public class ApplicationSectionRulesTests
{
    [Fact]
    public void ValidateApplicantInfo_BlankFields_ReturnsKeyedErrors()
    {
        var errors = ApplicationSectionRules.ValidateApplicantInfo(" ", null, "bad", "");

        Assert.Contains("FullName", errors.Keys);
        Assert.Contains("Phone", errors.Keys);
        Assert.Contains("Email", errors.Keys);
        Assert.Contains("CurrentAddress", errors.Keys);
    }

    [Fact]
    public void ValidateApplicantInfo_OverBusinessMax_ReturnsLengthErrors()
    {
        var errors = ApplicationSectionRules.ValidateApplicantInfo(
            new string('A', 201),
            "555",
            "a@b.co",
            new string('B', 501));

        Assert.Contains("FullName", errors.Keys);
        Assert.Contains("CurrentAddress", errors.Keys);
        Assert.DoesNotContain("Phone", errors.Keys);
    }

    [Fact]
    public void ValidateResidenceHistory_Empty_ReturnsResidencesKey()
    {
        var errors = ApplicationSectionRules.ValidateResidenceHistory([]);

        Assert.True(errors.ContainsKey("Residences"));
    }

    [Fact]
    public void ValidateResidenceHistory_InvalidRow_UsesIndexedKeys()
    {
        var errors = ApplicationSectionRules.ValidateResidenceHistory(
        [
            new ResidenceInput(null, "Landlord", "555", new DateOnly(2020, 1, 1), new DateOnly(2019, 1, 1))
        ]);

        Assert.Contains("Residences[0].Address", errors.Keys);
        Assert.Contains("Residences[0].MoveOutDate", errors.Keys);
    }

    [Fact]
    public void GetSubmissionBlockers_AggregatesBothSections()
    {
        var blockers = ApplicationSectionRules.GetSubmissionBlockers(
            null, "555", "a@b.co", "Addr",
            []);

        Assert.Contains(blockers, b => b.Section == ApplicationWizardSection.ApplicantInfo && b.Field == "FullName");
        Assert.Contains(blockers, b => b.Section == ApplicationWizardSection.ResidenceHistory && b.Field == "Residences");
    }
}
