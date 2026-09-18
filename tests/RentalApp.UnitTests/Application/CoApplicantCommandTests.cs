using RentalApp.Application.Applications;
using RentalApp.Domain.Entities;
using RentalApp.Domain.Enums;
using RentalApp.UnitTests.Support;

namespace RentalApp.UnitTests.Application;

public class CoApplicantCommandTests
{
    [Fact]
    public async Task AddCoApplicant_Member_AddsTarget()
    {
        var repository = new ApplicationRepositoryFake();
        var service = new ApplicationCommandService(repository);

        var result = await service.AddCoApplicantAsync(new AddCoApplicantCommand(1, "owner", "co-app"));

        Assert.True(result.IsSuccess);
        Assert.Contains(repository.Application.Applicants, a => a.UserId == "co-app");
    }

    [Fact]
    public async Task RemoveCoApplicant_CannotRemoveCreator()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.Applicants.Add(new ApplicationApplicant { UserId = "co-app", RentalApplicationId = 1 });
        var service = new ApplicationCommandService(repository);

        var result = await service.RemoveCoApplicantAsync(new RemoveCoApplicantCommand(1, "owner", "owner"));

        Assert.True(result.IsFailure);
        Assert.Contains(repository.Application.Applicants, a => a.UserId == "owner");
    }

    [Fact]
    public async Task CoApplicant_CanSaveApplicantInfo()
    {
        var repository = new ApplicationRepositoryFake();
        repository.Application.Applicants.Add(new ApplicationApplicant { UserId = "co-app", RentalApplicationId = 1 });
        repository.Application.CurrentSection = global::RentalApp.Domain.Enums.ApplicationWizardSection.ApplicantInfo;
        repository.Application.ApplicantInfoSaved = false;
        var service = new ApplicationCommandService(repository);

        var result = await service.SaveApplicantInfoAsync(new SaveApplicantInfoCommand(
            1, "co-app", false, "Co Name", "555-0111", "co@example.test", "Addr", true, 0));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsValid);
        Assert.Equal("Co Name", repository.Application.FullName);
    }
}
