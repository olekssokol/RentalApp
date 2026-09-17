using System.ComponentModel.DataAnnotations;
using RentalApp.Domain.Common;
using RentalApp.Domain.Entities;
using RentalApp.Domain.Enums;
using RentalApp.Domain.Services;

namespace RentalApp.Application.Applications;

public class ApplicationCommandService : IApplicationCommandService
{
    private readonly IRentalApplicationRepository _applications;

    public ApplicationCommandService(IRentalApplicationRepository applications) =>
        _applications = applications;

    public async Task<Result<int>> StartAsync(StartApplicationCommand command, CancellationToken ct = default)
    {
        var unit = await _applications.GetUnitWithLeasesAsync(command.UnitId, ct);
        if (unit is null)
            return Result.Failure<int>("Unit not found.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!UnitAvailability.IsAvailable(unit.Leases, today))
            return Result.Failure<int>("This unit is not available.");

        var application = new RentalApplication
        {
            ApplicantUserId = command.ApplicantUserId,
            UnitId = command.UnitId,
            Status = ApplicationStatus.Draft,
            CurrentSection = ApplicationWizardSection.ApplicantInfo
        };
        await _applications.AddAsync(application, ct);
        await _applications.SaveChangesAsync(ct);

        AddHistory(application, null, ApplicationStatus.Draft, command.ApplicantUserId, command.ApplicantDisplayName, "Application started.");
        await _applications.SaveChangesAsync(ct);
        return Result.Success(application.Id);
    }

    public async Task<Result> SaveApplicantInfoAsync(SaveApplicantInfoCommand command, CancellationToken ct = default)
    {
        var application = await _applications.GetByIdAsync(command.ApplicationId, ct);
        if (application is null)
            return Result.Failure("Application not found.");

        var access = EnsureApplicantAccess(application, command.UserId, command.IsManager);
        if (access.IsFailure)
            return access;

        var edit = ApplicationRules.EnsureCanEdit(application.Status);
        if (edit.IsFailure)
            return edit;

        if (string.IsNullOrWhiteSpace(command.FullName) || string.IsNullOrWhiteSpace(command.Phone) ||
            string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.CurrentAddress))
            return Result.Failure("All applicant information fields are required.");

        if (command.FullName.Length > 200)
            return Result.Failure("Full name cannot exceed 200 characters.");
        if (command.Phone.Length > 50)
            return Result.Failure("Phone cannot exceed 50 characters.");
        if (command.Email.Length > 256)
            return Result.Failure("Email cannot exceed 256 characters.");
        if (command.CurrentAddress.Length > 500)
            return Result.Failure("Current address cannot exceed 500 characters.");
        if (!new EmailAddressAttribute().IsValid(command.Email))
            return Result.Failure("Enter a valid email address.");

        application.FullName = command.FullName.Trim();
        application.Phone = command.Phone.Trim();
        application.Email = command.Email.Trim();
        application.CurrentAddress = command.CurrentAddress.Trim();
        application.ApplicantInfoSaved = true;
        application.UpdatedAtUtc = DateTime.UtcNow;

        if (command.Advance)
            application.CurrentSection = ApplicationWizardSection.ResidenceHistory;

        await _applications.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SaveResidenceHistoryAsync(SaveResidenceHistoryCommand command, CancellationToken ct = default)
    {
        var application = await _applications.GetWithResidencesAsync(command.ApplicationId, ct);
        if (application is null)
            return Result.Failure("Application not found.");

        var access = EnsureApplicantAccess(application, command.UserId, command.IsManager);
        if (access.IsFailure)
            return access;

        var edit = ApplicationRules.EnsureCanEdit(application.Status);
        if (edit.IsFailure)
            return edit;

        if (application.Residences.Count == 0)
            return Result.Failure("Add at least one prior residence before continuing.");

        application.ResidenceHistorySaved = true;
        application.UpdatedAtUtc = DateTime.UtcNow;
        if (command.Advance)
            application.CurrentSection = ApplicationWizardSection.Summary;

        await _applications.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> GoBackAsync(GoBackCommand command, CancellationToken ct = default)
    {
        var application = await _applications.GetByIdAsync(command.ApplicationId, ct);
        if (application is null)
            return Result.Failure("Application not found.");

        var access = EnsureApplicantAccess(application, command.UserId, command.IsManager);
        if (access.IsFailure)
            return access;

        var edit = ApplicationRules.EnsureCanEdit(application.Status);
        if (edit.IsFailure)
            return edit;

        application.CurrentSection = application.CurrentSection switch
        {
            ApplicationWizardSection.ResidenceHistory => ApplicationWizardSection.ApplicantInfo,
            ApplicationWizardSection.Summary => ApplicationWizardSection.ResidenceHistory,
            _ => ApplicationWizardSection.ApplicantInfo
        };
        application.UpdatedAtUtc = DateTime.UtcNow;
        await _applications.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SubmitAsync(SubmitApplicationCommand command, CancellationToken ct = default)
    {
        var application = await _applications.GetWithUnitLeasesAsync(command.ApplicationId, ct);
        if (application is null)
            return Result.Failure("Application not found.");

        if (application.ApplicantUserId != command.UserId)
            return Result.Failure("You do not own this application.");

        if (application.CurrentSection != ApplicationWizardSection.Summary)
            return Result.Failure("Submit is only available from the summary.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var hasLease = UnitAvailability.HasActiveLease(application.Unit.Leases, today);
        var check = ApplicationRules.EnsureCanSubmit(
            application.Status, application.ApplicantInfoSaved, application.ResidenceHistorySaved, hasLease);
        if (check.IsFailure)
            return check;

        var from = application.Status;
        application.Status = ApplicationStatus.Submitted;
        application.CurrentSection = ApplicationWizardSection.Summary;
        application.UpdatedAtUtc = DateTime.UtcNow;
        AddHistory(application, from, ApplicationStatus.Submitted, command.UserId, application.FullName ?? "Applicant", "Application submitted.");
        await _applications.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> WithdrawAsync(WithdrawApplicationCommand command, CancellationToken ct = default)
    {
        var application = await _applications.GetByIdAsync(command.ApplicationId, ct);
        if (application is null)
            return Result.Failure("Application not found.");

        if (application.ApplicantUserId != command.UserId)
            return Result.Failure("You do not own this application.");

        if (!ApplicationRules.CanWithdraw(application.Status))
            return Result.Failure("This application cannot be withdrawn.");

        var from = application.Status;
        application.Status = ApplicationStatus.Withdrawn;
        application.UpdatedAtUtc = DateTime.UtcNow;
        AddHistory(application, from, ApplicationStatus.Withdrawn, command.UserId, application.FullName ?? "Applicant", "Application withdrawn.");
        await _applications.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<int>> AddResidenceAsync(AddResidenceCommand command, CancellationToken ct = default)
    {
        var application = await GetEditableApplicationAsync(command.ApplicationId, command.UserId, command.IsManager, ct);
        if (application.IsFailure)
            return Result.Failure<int>(application.Error!);

        var validation = ValidateResidence(command.Address, command.LandlordName, command.LandlordPhone, command.MoveInDate, command.MoveOutDate);
        if (validation.IsFailure)
            return Result.Failure<int>(validation.Error!);

        var residence = new ResidenceHistory
        {
            RentalApplicationId = command.ApplicationId,
            Address = command.Address.Trim(),
            LandlordName = command.LandlordName.Trim(),
            LandlordPhone = command.LandlordPhone.Trim(),
            MoveInDate = command.MoveInDate,
            MoveOutDate = command.MoveOutDate
        };
        await _applications.AddResidenceAsync(residence, ct);
        application.Value!.UpdatedAtUtc = DateTime.UtcNow;
        await _applications.SaveChangesAsync(ct);
        return Result.Success(residence.Id);
    }

    public async Task<Result> UpdateResidenceAsync(UpdateResidenceCommand command, CancellationToken ct = default)
    {
        var application = await GetEditableApplicationAsync(command.ApplicationId, command.UserId, command.IsManager, ct);
        if (application.IsFailure)
            return Result.Failure(application.Error!);

        var validation = ValidateResidence(command.Address, command.LandlordName, command.LandlordPhone, command.MoveInDate, command.MoveOutDate);
        if (validation.IsFailure)
            return validation;

        var residence = await _applications.GetResidenceAsync(command.ApplicationId, command.ResidenceId, ct);
        if (residence is null)
            return Result.Failure("Residence not found.");

        residence.Address = command.Address.Trim();
        residence.LandlordName = command.LandlordName.Trim();
        residence.LandlordPhone = command.LandlordPhone.Trim();
        residence.MoveInDate = command.MoveInDate;
        residence.MoveOutDate = command.MoveOutDate;
        application.Value!.UpdatedAtUtc = DateTime.UtcNow;
        await _applications.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteResidenceAsync(DeleteResidenceCommand command, CancellationToken ct = default)
    {
        var application = await GetEditableApplicationAsync(command.ApplicationId, command.UserId, command.IsManager, ct);
        if (application.IsFailure)
            return Result.Failure(application.Error!);

        var residence = await _applications.GetResidenceAsync(command.ApplicationId, command.ResidenceId, ct);
        if (residence is null)
            return Result.Failure("Residence not found.");

        _applications.RemoveResidence(residence);
        application.Value!.UpdatedAtUtc = DateTime.UtcNow;
        await _applications.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ReviewAsync(ReviewApplicationCommand command, CancellationToken ct = default)
    {
        var commentCheck = ApplicationRules.EnsureReviewComment(command.Outcome, command.Comment);
        if (commentCheck.IsFailure)
            return commentCheck;

        if (command.Outcome == ReviewOutcome.Approve)
            return await ApproveAsync(command, ct);

        var application = await _applications.GetByIdAsync(command.ApplicationId, ct);
        if (application is null)
            return Result.Failure("Application not found.");

        var reviewCheck = ApplicationRules.EnsureCanReview(application.Status);
        if (reviewCheck.IsFailure)
            return reviewCheck;

        ApplyReviewOutcome(application, command);
        await _applications.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> ApproveAsync(ReviewApplicationCommand command, CancellationToken ct)
    {
        await using var transaction = await _applications.BeginSerializableAsync(ct);
        try
        {
            var application = await _applications.GetWithUnitLeasesAsync(command.ApplicationId, ct);
            if (application is null)
                return Result.Failure("Application not found.");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var approveCheck = ApplicationRules.EnsureCanApprove(
                application.Status, UnitAvailability.HasActiveLease(application.Unit.Leases, today));
            if (approveCheck.IsFailure)
                return approveCheck;

            var (start, end) = ApplicationRules.CreateTwelveMonthLeaseTerm(today);
            _applications.AddLease(new Lease
            {
                UnitId = application.UnitId,
                RentalApplicationId = application.Id,
                StartDate = start,
                EndDate = end
            });

            ApplyReviewOutcome(application, command);
            await _applications.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) when (_applications.IsSerializationFailure(ex))
        {
            return Result.Failure("This unit already has an active lease. Approval is not allowed.");
        }
    }

    private void ApplyReviewOutcome(RentalApplication application, ReviewApplicationCommand command)
    {
        var from = application.Status;
        var to = ApplicationRules.MapOutcomeToStatus(command.Outcome);
        application.Status = to;
        application.UpdatedAtUtc = DateTime.UtcNow;

        if (command.Outcome == ReviewOutcome.Return)
            application.CurrentSection = ApplicationWizardSection.ApplicantInfo;

        AddHistory(application, from, to, command.ManagerUserId, command.ManagerDisplayName, command.Comment);
    }

    private static Result EnsureApplicantAccess(RentalApplication application, string userId, bool isManager)
    {
        if (isManager)
            return Result.Success();

        return application.ApplicantUserId == userId
            ? Result.Success()
            : Result.Failure("You do not own this application.");
    }

    private async Task<Result<RentalApplication>> GetEditableApplicationAsync(
        int applicationId, string userId, bool isManager, CancellationToken ct)
    {
        var application = await _applications.GetByIdAsync(applicationId, ct);
        if (application is null)
            return Result.Failure<RentalApplication>("Application not found.");

        var access = EnsureApplicantAccess(application, userId, isManager);
        if (access.IsFailure)
            return Result.Failure<RentalApplication>(access.Error!);

        var edit = ApplicationRules.EnsureCanEdit(application.Status);
        if (edit.IsFailure)
            return Result.Failure<RentalApplication>(edit.Error!);

        return Result.Success(application);
    }

    private static Result ValidateResidence(
        string address, string landlordName, string landlordPhone, DateOnly moveIn, DateOnly? moveOut)
    {
        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(landlordName) || string.IsNullOrWhiteSpace(landlordPhone))
            return Result.Failure("Address, landlord name, and landlord phone are required.");

        if (moveIn == DateOnly.MinValue)
            return Result.Failure("Move-in date is required.");

        if (moveOut.HasValue && moveOut.Value < moveIn)
            return Result.Failure("Move-out date cannot be before move-in date.");

        return Result.Success();
    }

    private void AddHistory(
        RentalApplication application,
        ApplicationStatus? from,
        ApplicationStatus to,
        string userId,
        string displayName,
        string? comment)
    {
        _applications.AddStatusHistory(new ApplicationStatusHistory
        {
            RentalApplicationId = application.Id,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = userId,
            ChangedByDisplayName = displayName,
            Comment = comment
        });
    }
}
