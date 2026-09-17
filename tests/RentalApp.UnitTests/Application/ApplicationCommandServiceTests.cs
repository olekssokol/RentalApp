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

        Result result = operation switch
        {
            "SaveApplicant" => await service.SaveApplicantInfoAsync(ValidApplicant("foreign")),
            "SaveResidences" => await service.SaveResidenceHistoryAsync(new(1, "foreign", false, true)),
            "AddResidence" => await service.AddResidenceAsync(new(1, "foreign", false, "Changed", "Landlord", "123", new(2020, 1, 1), null)),
            "UpdateResidence" => await service.UpdateResidenceAsync(new(1, 1, "foreign", false, "Changed", "Landlord", "123", new(2020, 1, 1), null)),
            "DeleteResidence" => await service.DeleteResidenceAsync(new(1, 1, "foreign", false)),
            "Back" => await service.GoBackAsync(new(1, "foreign", false)),
            "Submit" => await service.SubmitAsync(new(1, "foreign")),
            "Withdraw" => await service.WithdrawAsync(new(1, "foreign")),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        Assert.True(result.IsFailure);
        Assert.Equal(before, Snapshot(repository.Application));
        Assert.Equal(0, repository.SaveCount);
        Assert.Empty(repository.History);
    }

    [Theory]
    [InlineData(ApplicationWizardSection.ApplicantInfo)]
    [InlineData(ApplicationWizardSection.ResidenceHistory)]
    public async Task SubmitAsync_BeforeSummary_DoesNotSubmitEvenWithSavedSections(ApplicationWizardSection section)
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.CurrentSection = section;
        var service = new ApplicationCommandService(repository);

        var result = await service.SubmitAsync(new(1, "owner"));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationStatus.Draft, repository.Application.Status);
        Assert.Equal(section, repository.Application.CurrentSection);
        Assert.Equal(0, repository.SaveCount);
        Assert.Empty(repository.History);
    }

    [Theory]
    [InlineData("FullName", 201)]
    [InlineData("Phone", 51)]
    [InlineData("Email", 257)]
    [InlineData("CurrentAddress", 501)]
    [InlineData("InvalidEmail", 0)]
    [InlineData("BlankName", 0)]
    public async Task SaveApplicantInfoAsync_InvalidInput_DoesNotSaveOrAdvance(string field, int length)
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.CurrentSection = ApplicationWizardSection.ApplicantInfo;
        var before = Snapshot(repository.Application);
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

        Assert.True(result.IsFailure);
        Assert.Equal(before, Snapshot(repository.Application));
        Assert.Equal(0, repository.SaveCount);
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
        Assert.Equal("New applicant", repository.Application.FullName);
        Assert.Equal("555-0111", repository.Application.Phone);
        Assert.Equal("New address", repository.Application.CurrentAddress);
        Assert.True(repository.Application.ApplicantInfoSaved);
        Assert.False(repository.Application.ResidenceHistorySaved);
        Assert.Equal(ApplicationWizardSection.ResidenceHistory, repository.Application.CurrentSection);
        Assert.True(repository.SaveCount > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResidenceCommand_DefaultMoveInDate_DoesNotAddOrOverwriteResidence(bool update)
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.Residences.Add(Residence());
        var before = Snapshot(repository.Application);
        var service = new ApplicationCommandService(repository);

        Result result = update
            ? await service.UpdateResidenceAsync(new(1, 1, "owner", false, "Changed", "New landlord", "123", default, null))
            : await service.AddResidenceAsync(new(1, "owner", false, "Changed", "New landlord", "123", default, null));

        Assert.True(result.IsFailure);
        Assert.Equal(before, Snapshot(repository.Application));
        Assert.Equal(0, repository.SaveCount);
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
        var service = new ApplicationCommandService(repository);

        var result = await service.SaveResidenceHistoryAsync(new(1, "owner", false, true));

        Assert.Equal(hasResidence, result.IsSuccess);
        Assert.Equal(hasResidence, repository.Application.ResidenceHistorySaved);
        Assert.Equal(hasResidence ? ApplicationWizardSection.Summary : ApplicationWizardSection.ResidenceHistory, repository.Application.CurrentSection);
        Assert.Equal(hasResidence, repository.SaveCount > 0);
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
        repository.Application.Status = ApplicationStatus.Submitted;
        var service = new ApplicationCommandService(repository);

        var result = await service.ReviewAsync(new(1, "manager", "Pat Manager", outcome, "Review reason"));

        Assert.True(result.IsSuccess);
        Assert.Equal(status, repository.Application.Status);
        Assert.Equal(section, repository.Application.CurrentSection);
        var history = Assert.Single(repository.History);
        Assert.Equal(ApplicationStatus.Submitted, history.FromStatus);
        Assert.Equal(status, history.ToStatus);
        Assert.Equal("manager", history.ChangedByUserId);
        Assert.Equal("Pat Manager", history.ChangedByDisplayName);
        Assert.Equal("Review reason", history.Comment);
        Assert.True(repository.SaveCount > 0);
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

    private static SaveApplicantInfoCommand ValidApplicant(string user = "owner") =>
        new(1, user, false, "New applicant", "555-0111", "applicant@example.test", "New address", true);

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
        Residences = application.Residences.Select(r => new { r.Id, r.Address, r.LandlordName, r.LandlordPhone, r.MoveInDate, r.MoveOutDate })
    });
}
