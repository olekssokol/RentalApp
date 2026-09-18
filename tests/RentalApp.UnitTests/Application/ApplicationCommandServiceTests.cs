using RentalApp.Application.Applications;
using RentalApp.Domain.Common;
using RentalApp.Domain.Entities;
using RentalApp.Domain.Enums;
using RentalApp.UnitTests.Support;

namespace RentalApp.UnitTests.Application;

public class ApplicationCommandServiceTests
{
    [Theory]
    [InlineData("SaveApplicant")]
    [InlineData("SaveResidences")]
    [InlineData("AddResidence")]
    [InlineData("UpdateResidence")]
    [InlineData("DeleteResidence")]
    [InlineData("Back")]
    [InlineData("Submit")]
    [InlineData("Withdraw")]
    public async Task ApplicantCommand_ForeignOwner_DoesNotMutateOrSave(string operation)
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.Residences.Add(Residence());
        var before = Snapshot(repository.Application);
        var service = new ApplicationCommandService(repository);

        var failed = operation switch
        {
            "SaveApplicant" => (await service.SaveApplicantInfoAsync(ValidApplicant("foreign"))).IsFailure,
            "SaveResidences" => (await service.SaveResidenceHistoryAsync(new(1, "foreign", false, true, 0))).IsFailure,
            "AddResidence" => (await service.AddResidenceAsync(new(1, "foreign", false, "Changed", "Landlord", "123", new(2020, 1, 1), null, 0))).IsFailure,
            "UpdateResidence" => (await service.UpdateResidenceAsync(new(1, 1, "foreign", false, "Changed", "Landlord", "123", new(2020, 1, 1), null, 0))).IsFailure,
            "DeleteResidence" => (await service.DeleteResidenceAsync(new(1, 1, "foreign", false, 0))).IsFailure,
            "Back" => (await service.GoBackAsync(new(1, "foreign", false))).IsFailure,
            "Submit" => (await service.SubmitAsync(new(1, "foreign"))).IsFailure,
            "Withdraw" => (await service.WithdrawAsync(new(1, "foreign"))).IsFailure,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        Assert.True(failed);
        Assert.Equal(before, Snapshot(repository.Application));
        Assert.Equal(0, repository.SaveCount);
        Assert.Empty(repository.History);
    }

    [Theory]
    [InlineData(ApplicationWizardSection.ApplicantInfo)]
    [InlineData(ApplicationWizardSection.ResidenceHistory)]
    public async Task SubmitAsync_ValidData_SucceedsRegardlessOfPersistedCurrentSection(ApplicationWizardSection section)
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.CurrentSection = section;
        repository.Application.Residences.Add(Residence());
        var service = new ApplicationCommandService(repository);

        var result = await service.SubmitAsync(new(1, "owner"));

        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationStatus.Submitted, repository.Application.Status);
        Assert.Equal(ApplicationWizardSection.Summary, repository.Application.CurrentSection);
    }

    [Theory]
    [InlineData("FullName", 201)]
    [InlineData("Phone", 51)]
    [InlineData("Email", 257)]
    [InlineData("CurrentAddress", 501)]
    [InlineData("InvalidEmail", 0)]
    [InlineData("BlankName", 0)]
    public async Task SaveApplicantInfoAsync_InvalidInput_PersistsWithoutAdvancing(string field, int length)
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.CurrentSection = ApplicationWizardSection.ApplicantInfo;
        repository.Application.ApplicantInfoSaved = true;
        var service = new ApplicationCommandService(repository);
        var valid = ValidApplicant();
        var command = field switch
        {
            "FullName" => valid with { FullName = new string('A', length) },
            "Phone" => valid with { Phone = new string('1', length) },
            "Email" => valid with { Email = new string('e', length - 12) + "@example.com" },
            "CurrentAddress" => valid with { CurrentAddress = new string('A', length) },
            "InvalidEmail" => valid with { Email = "not-an-email" },
            "BlankName" => valid with { FullName = " \t " },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        var result = await service.SaveApplicantInfoAsync(command);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsValid);
        Assert.NotEmpty(result.Value.FieldErrors);
        Assert.False(repository.Application.ApplicantInfoSaved);
        Assert.Equal(ApplicationWizardSection.ApplicantInfo, repository.Application.CurrentSection);
        Assert.True(repository.SaveCount > 0);
        Assert.Equal(ApplicationSectionRulesNormalize(command.FullName), repository.Application.FullName);
    }

    [Fact]
    public async Task SaveApplicantInfoAsync_ValidThenInvalid_ResetsSavedFlag()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.CurrentSection = ApplicationWizardSection.ApplicantInfo;
        repository.Application.ApplicantInfoSaved = false;
        var service = new ApplicationCommandService(repository);

        var valid = await service.SaveApplicantInfoAsync(ValidApplicant() with { Advance = false });
        Assert.True(valid.Value!.IsValid);
        Assert.True(repository.Application.ApplicantInfoSaved);

        var invalid = await service.SaveApplicantInfoAsync(ValidApplicant() with
        {
            FullName = "",
            Advance = true,
            ExpectedApplicantInfoVersion = 1
        });
        Assert.True(invalid.IsSuccess);
        Assert.False(invalid.Value!.IsValid);
        Assert.False(repository.Application.ApplicantInfoSaved);
        Assert.Equal(ApplicationWizardSection.ApplicantInfo, repository.Application.CurrentSection);
        Assert.Null(repository.Application.FullName);
    }

    [Fact]
    public async Task SaveApplicantInfoAsync_ValidInput_TrimsSavesAndAdvancesOnlyApplicantSection()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.CurrentSection = ApplicationWizardSection.ApplicantInfo;
        repository.Application.ApplicantInfoSaved = false;
        repository.Application.ResidenceHistorySaved = false;
        var service = new ApplicationCommandService(repository);

        var result = await service.SaveApplicantInfoAsync(ValidApplicant() with
        {
            FullName = "  New applicant  ", Phone = " 555-0111 ", CurrentAddress = " New address "
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsValid);
        Assert.Equal("New applicant", repository.Application.FullName);
        Assert.Equal("555-0111", repository.Application.Phone);
        Assert.Equal("New address", repository.Application.CurrentAddress);
        Assert.True(repository.Application.ApplicantInfoSaved);
        Assert.False(repository.Application.ResidenceHistorySaved);
        Assert.Equal(ApplicationWizardSection.ResidenceHistory, repository.Application.CurrentSection);
        Assert.True(repository.SaveCount > 0);
    }

    [Fact]
    public async Task AddResidenceAsync_InvalidFields_PersistsWithIndexedErrors()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.CurrentSection = ApplicationWizardSection.ResidenceHistory;
        repository.Application.ResidenceHistorySaved = true;
        var service = new ApplicationCommandService(repository);

        var result = await service.AddResidenceAsync(new(1, "owner", false, "", "Landlord", "123", null, null, 0));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsValid);
        Assert.Contains("Residences[0].Address", result.Value.FieldErrors.Keys);
        Assert.Contains("Residences[0].MoveInDate", result.Value.FieldErrors.Keys);
        Assert.False(repository.Application.ResidenceHistorySaved);
        Assert.Single(repository.Application.Residences);
        Assert.Null(Assert.Single(repository.Application.Residences).Address);
        Assert.True(repository.SaveCount > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveResidenceHistoryAsync_ResidencePresence_ControlsAdvancement(bool hasResidence)
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.CurrentSection = ApplicationWizardSection.ResidenceHistory;
        repository.Application.ResidenceHistorySaved = false;
        if (hasResidence) repository.Application.Residences.Add(Residence());
        var updatedAt = repository.Application.UpdatedAtUtc;
        var service = new ApplicationCommandService(repository);

        var result = await service.SaveResidenceHistoryAsync(new(1, "owner", false, true, 0));

        Assert.True(result.IsSuccess);
        Assert.Equal(hasResidence, result.Value!.IsValid);
        Assert.Equal(hasResidence, repository.Application.ResidenceHistorySaved);
        Assert.Equal(hasResidence ? ApplicationWizardSection.Summary : ApplicationWizardSection.ResidenceHistory, repository.Application.CurrentSection);
        if (hasResidence)
        {
            Assert.True(repository.SaveCount > 0);
        }
        else
        {
            Assert.Equal(0, repository.SaveCount);
            Assert.Equal(updatedAt, repository.Application.UpdatedAtUtc);
            Assert.Contains("Residences", result.Value.FieldErrors.Keys);
        }
    }

    [Fact]
    public async Task SubmitAsync_StaleSavedFlagsWithInvalidData_Rejects()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.FullName = null;
        repository.Application.ApplicantInfoSaved = true;
        repository.Application.ResidenceHistorySaved = true;
        repository.Application.Residences.Add(Residence());
        var service = new ApplicationCommandService(repository);

        var result = await service.SubmitAsync(new(1, "owner"));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationStatus.Draft, repository.Application.Status);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task SubmitAsync_ValidPersistedState_Succeeds()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.Residences.Add(Residence());
        var service = new ApplicationCommandService(repository);

        var result = await service.SubmitAsync(new(1, "owner"));

        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationStatus.Submitted, repository.Application.Status);
    }

    [Theory]
    [InlineData(ApplicationWizardSection.ResidenceHistory, ApplicationWizardSection.ApplicantInfo)]
    [InlineData(ApplicationWizardSection.Summary, ApplicationWizardSection.ResidenceHistory)]
    public async Task GoBackAsync_EditableApplication_PreservesSavedBusinessData(ApplicationWizardSection from, ApplicationWizardSection to)
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.CurrentSection = from;
        repository.Application.Residences.Add(Residence());
        var service = new ApplicationCommandService(repository);

        var result = await service.GoBackAsync(new(1, "owner", false));

        Assert.True(result.IsSuccess);
        Assert.Equal(to, repository.Application.CurrentSection);
        Assert.Equal("Original applicant", repository.Application.FullName);
        Assert.Equal("Original address", repository.Application.CurrentAddress);
        Assert.Equal("Prior address", Assert.Single(repository.Application.Residences).Address);
        Assert.True(repository.Application.ApplicantInfoSaved);
        Assert.True(repository.Application.ResidenceHistorySaved);
        Assert.Equal(ApplicationStatus.Draft, repository.Application.Status);
        Assert.Empty(repository.History);
    }

    [Theory]
    [InlineData(ApplicationStatus.Draft)]
    [InlineData(ApplicationStatus.Returned)]
    public async Task SubmitAsync_CompleteEditableApplication_ChangesStatusAndRecordsActor(ApplicationStatus status)
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.Status = status;
        repository.Application.Residences.Add(Residence());
        var service = new ApplicationCommandService(repository);

        var result = await service.SubmitAsync(new(1, "owner"));

        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationStatus.Submitted, repository.Application.Status);
        var history = Assert.Single(repository.History);
        Assert.Equal(status, history.FromStatus);
        Assert.Equal(ApplicationStatus.Submitted, history.ToStatus);
        Assert.Equal("owner", history.ChangedByUserId);
        Assert.True(repository.SaveCount > 0);
    }

    [Theory]
    [InlineData(ReviewOutcome.Return, ApplicationStatus.Returned, ApplicationWizardSection.ApplicantInfo)]
    [InlineData(ReviewOutcome.Deny, ApplicationStatus.Denied, ApplicationWizardSection.Summary)]
    public async Task ReviewAsync_ValidDecision_RecordsReviewAndSetsWorkflow(
        ReviewOutcome outcome, ApplicationStatus status, ApplicationWizardSection section)
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.Status = ApplicationStatus.UnderReview;
        repository.Application.ClaimedByUserId = "manager";
        repository.Application.ClaimedAtUtc = DateTime.UtcNow;
        var service = new ApplicationCommandService(repository);

        var result = await service.ReviewAsync(new(1, "manager", "Pat Manager", outcome, "Review reason"));

        Assert.True(result.IsSuccess);
        Assert.Equal(status, repository.Application.Status);
        Assert.Equal(section, repository.Application.CurrentSection);
        Assert.Null(repository.Application.ClaimedByUserId);
        Assert.Null(repository.Application.ClaimedAtUtc);
        var history = Assert.Single(repository.History);
        Assert.Equal(ApplicationStatus.UnderReview, history.FromStatus);
        Assert.Equal(status, history.ToStatus);
        Assert.Equal("manager", history.ChangedByUserId);
        Assert.Equal("Pat Manager", history.ChangedByDisplayName);
        Assert.Equal("Review reason", history.Comment);
        Assert.True(repository.SaveCount > 0);
    }

    [Fact]
    public async Task ClaimAsync_SubmittedApplication_MovesToUnderReviewAndRecordsHistory()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.Status = ApplicationStatus.Submitted;
        var service = new ApplicationCommandService(repository);

        var result = await service.ClaimAsync(new(1, "manager", "Pat Manager"));

        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationStatus.UnderReview, repository.Application.Status);
        Assert.Equal("manager", repository.Application.ClaimedByUserId);
        Assert.NotNull(repository.Application.ClaimedAtUtc);
        var history = Assert.Single(repository.History);
        Assert.Equal(ApplicationStatus.Submitted, history.FromStatus);
        Assert.Equal(ApplicationStatus.UnderReview, history.ToStatus);
        Assert.Equal("Claimed for review", history.Comment);
    }

    [Fact]
    public async Task ReleaseAsync_Claimer_ReturnsToSubmittedAndClearsClaim()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.Status = ApplicationStatus.UnderReview;
        repository.Application.ClaimedByUserId = "manager";
        repository.Application.ClaimedAtUtc = DateTime.UtcNow;
        var service = new ApplicationCommandService(repository);

        var result = await service.ReleaseAsync(new(1, "manager", "Pat Manager"));

        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationStatus.Submitted, repository.Application.Status);
        Assert.Null(repository.Application.ClaimedByUserId);
        Assert.Null(repository.Application.ClaimedAtUtc);
        var history = Assert.Single(repository.History);
        Assert.Equal(ApplicationStatus.UnderReview, history.FromStatus);
        Assert.Equal(ApplicationStatus.Submitted, history.ToStatus);
        Assert.Equal("Released back to review queue", history.Comment);
    }

    [Fact]
    public async Task ReviewAsync_SubmittedWithoutClaim_DoesNotMutate()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.Status = ApplicationStatus.Submitted;
        var before = Snapshot(repository.Application);
        var service = new ApplicationCommandService(repository);

        var result = await service.ReviewAsync(new(1, "manager", "Pat Manager", ReviewOutcome.Deny, "No"));

        Assert.True(result.IsFailure);
        Assert.Equal(before, Snapshot(repository.Application));
        Assert.Equal(0, repository.SaveCount);
        Assert.Empty(repository.History);
    }

    [Fact]
    public async Task ReviewAsync_OtherManager_DoesNotMutate()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.Status = ApplicationStatus.UnderReview;
        repository.Application.ClaimedByUserId = "manager";
        repository.Application.ClaimedAtUtc = DateTime.UtcNow;
        var before = Snapshot(repository.Application);
        var service = new ApplicationCommandService(repository);

        var result = await service.ReviewAsync(new(1, "other", "Other Manager", ReviewOutcome.Deny, "No"));

        Assert.True(result.IsFailure);
        Assert.Equal(before, Snapshot(repository.Application));
        Assert.Equal(0, repository.SaveCount);
        Assert.Empty(repository.History);
    }

    [Fact]
    public async Task WithdrawAsync_UnderReview_IsRejected()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.Status = ApplicationStatus.UnderReview;
        repository.Application.ClaimedByUserId = "manager";
        var service = new ApplicationCommandService(repository);

        var result = await service.WithdrawAsync(new(1, "owner"));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationStatus.UnderReview, repository.Application.Status);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task WithdrawAsync_OwnSubmittedApplication_PreservesSectionsAndRecordsWithdrawal()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.Status = ApplicationStatus.Submitted;
        var service = new ApplicationCommandService(repository);

        var result = await service.WithdrawAsync(new(1, "owner"));

        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationStatus.Withdrawn, repository.Application.Status);
        Assert.Equal("Original applicant", repository.Application.FullName);
        Assert.True(repository.Application.ApplicantInfoSaved);
        Assert.True(repository.Application.ResidenceHistorySaved);
        var history = Assert.Single(repository.History);
        Assert.Equal(ApplicationStatus.Submitted, history.FromStatus);
        Assert.Equal(ApplicationStatus.Withdrawn, history.ToStatus);
        Assert.Equal("owner", history.ChangedByUserId);
    }

    [Fact]
    public async Task SaveApplicantInfoAsync_FixInvalid_ClearsErrorsAndAdvances()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.CurrentSection = ApplicationWizardSection.ApplicantInfo;
        repository.Application.ApplicantInfoSaved = false;
        var service = new ApplicationCommandService(repository);

        var invalid = await service.SaveApplicantInfoAsync(ValidApplicant() with { FullName = "", Advance = true });
        Assert.False(invalid.Value!.IsValid);
        Assert.False(repository.Application.ApplicantInfoSaved);

        var fixedSave = await service.SaveApplicantInfoAsync(ValidApplicant() with { ExpectedApplicantInfoVersion = 1 });
        Assert.True(fixedSave.Value!.IsValid);
        Assert.Empty(fixedSave.Value.FieldErrors);
        Assert.True(repository.Application.ApplicantInfoSaved);
        Assert.Equal(ApplicationWizardSection.ResidenceHistory, repository.Application.CurrentSection);
    }

    [Fact]
    public async Task UpdateResidenceAsync_InvalidEdit_ResetsResidenceHistorySaved()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.CurrentSection = ApplicationWizardSection.ResidenceHistory;
        repository.Application.Residences.Add(Residence());
        repository.Application.ResidenceHistorySaved = true;
        var service = new ApplicationCommandService(repository);

        var result = await service.UpdateResidenceAsync(
            new(1, 1, "owner", false, "", "Landlord", "555", new DateOnly(2020, 1, 1), null, 0));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsValid);
        Assert.Contains("Residences[0].Address", result.Value.FieldErrors.Keys);
        Assert.False(repository.Application.ResidenceHistorySaved);
        Assert.Null(repository.Application.Residences.Single().Address);
    }

    [Fact]
    public async Task GoBackAsync_DoesNotValidateOrRewriteApplicantFields()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.CurrentSection = ApplicationWizardSection.ResidenceHistory;
        repository.Application.FullName = null;
        repository.Application.ApplicantInfoSaved = false;
        var service = new ApplicationCommandService(repository);

        var result = await service.GoBackAsync(new(1, "owner", false));

        Assert.True(result.IsSuccess);
        Assert.Null(repository.Application.FullName);
        Assert.False(repository.Application.ApplicantInfoSaved);
        Assert.Equal(ApplicationWizardSection.ApplicantInfo, repository.Application.CurrentSection);
    }

    [Fact]
    public async Task StartAsync_AvailableUnit_CreatesDraftAndIdentifiesCreator()
    {
        var repository = new ApplicationRepositoryFake();
        var service = new ApplicationCommandService(repository);

        var result = await service.StartAsync(new("owner", 1, "Alex Applicant"));

        Assert.True(result.IsSuccess);
        var application = Assert.IsType<RentalApplication>(repository.AddedApplication);
        Assert.Equal("owner", application.ApplicantUserId);
        Assert.Equal(1, application.UnitId);
        Assert.Equal(ApplicationStatus.Draft, application.Status);
        Assert.Equal(ApplicationWizardSection.ApplicantInfo, application.CurrentSection);
        var history = Assert.Single(repository.History);
        Assert.Equal(application.Id, history.RentalApplicationId);
        Assert.Null(history.FromStatus);
        Assert.Equal(ApplicationStatus.Draft, history.ToStatus);
        Assert.Equal("owner", history.ChangedByUserId);
        Assert.Equal("Alex Applicant", history.ChangedByDisplayName);
    }

    private static string? ApplicationSectionRulesNormalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SaveApplicantInfoCommand ValidApplicant(string user = "owner") =>
        new(1, user, false, "New applicant", "555-0111", "applicant@example.test", "New address", true, 0);

    private static ResidenceHistory Residence() => new()
    {
        Id = 1, RentalApplicationId = 1, Address = "Prior address", LandlordName = "Landlord", LandlordPhone = "555-0222",
        MoveInDate = new DateOnly(2020, 1, 1), MoveOutDate = new DateOnly(2024, 1, 1)
    };

    private static string Snapshot(RentalApplication application) => System.Text.Json.JsonSerializer.Serialize(new
    {
        application.Status,
        application.CurrentSection,
        application.FullName,
        application.Phone,
        application.Email,
        application.CurrentAddress,
        application.ApplicantInfoSaved,
        application.ResidenceHistorySaved,
        application.ClaimedByUserId,
        Residences = application.Residences.Select(r => new { r.Id, r.Address, r.LandlordName, r.LandlordPhone, r.MoveInDate, r.MoveOutDate })
    });
}
