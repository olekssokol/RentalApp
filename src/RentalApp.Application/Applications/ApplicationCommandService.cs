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

    public async Task<Result<SaveSectionResult>> SaveApplicantInfoAsync(SaveApplicantInfoCommand command, CancellationToken ct = default)
    {
        var application = await _applications.GetByIdAsync(command.ApplicationId, ct);
        if (application is null)
            return Result.Failure<SaveSectionResult>("Application not found.");

        var access = EnsureApplicantAccess(application, command.UserId, command.IsManager);
        if (access.IsFailure)
            return Result.Failure<SaveSectionResult>(access.Error!);

        var edit = ApplicationRules.EnsureCanEdit(application.Status);
        if (edit.IsFailure)
            return Result.Failure<SaveSectionResult>(edit.Error!);

        var fullName = ApplicationSectionRules.NormalizeOptional(command.FullName);
        var phone = ApplicationSectionRules.NormalizeOptional(command.Phone);
        var email = ApplicationSectionRules.NormalizeOptional(command.Email);
        var currentAddress = ApplicationSectionRules.NormalizeOptional(command.CurrentAddress);

        var storage = EnsureApplicantStorage(fullName, phone, email, currentAddress);
        if (storage.IsFailure)
            return Result.Failure<SaveSectionResult>(storage.Error!);

        application.FullName = fullName;
        application.Phone = phone;
        application.Email = email;
        application.CurrentAddress = currentAddress;

        var errors = ApplicationSectionRules.ValidateApplicantInfo(fullName, phone, email, currentAddress);
        var isValid = errors.Count == 0;
        application.ApplicantInfoSaved = isValid;
        application.UpdatedAtUtc = DateTime.UtcNow;

        if (isValid && command.Advance)
            application.CurrentSection = ApplicationWizardSection.ResidenceHistory;

        await _applications.SaveChangesAsync(ct);
        return Result.Success(isValid ? SaveSectionResult.Valid() : SaveSectionResult.Invalid(errors));
    }

    public async Task<Result<SaveSectionResult>> SaveResidenceHistoryAsync(SaveResidenceHistoryCommand command, CancellationToken ct = default)
    {
        var application = await _applications.GetWithResidencesAsync(command.ApplicationId, ct);
        if (application is null)
            return Result.Failure<SaveSectionResult>("Application not found.");

        var access = EnsureApplicantAccess(application, command.UserId, command.IsManager);
        if (access.IsFailure)
            return Result.Failure<SaveSectionResult>(access.Error!);

        var edit = ApplicationRules.EnsureCanEdit(application.Status);
        if (edit.IsFailure)
            return Result.Failure<SaveSectionResult>(edit.Error!);

        var errors = ApplicationSectionRules.ValidateResidenceHistory(ToResidenceInputs(application.Residences));
        var isValid = errors.Count == 0;
        var wasSaved = application.ResidenceHistorySaved;
        application.ResidenceHistorySaved = isValid;

        if (isValid)
        {
            application.UpdatedAtUtc = DateTime.UtcNow;
            if (command.Advance)
                application.CurrentSection = ApplicationWizardSection.Summary;
            await _applications.SaveChangesAsync(ct);
            return Result.Success(SaveSectionResult.Valid());
        }

        // Invalid: stay on section. Persist only when the progress flag must flip true → false.
        if (wasSaved)
        {
            application.UpdatedAtUtc = DateTime.UtcNow;
            await _applications.SaveChangesAsync(ct);
        }

        return Result.Success(SaveSectionResult.Invalid(errors));
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

        var blockers = ApplicationSectionRules.GetSubmissionBlockers(
            application.FullName,
            application.Phone,
            application.Email,
            application.CurrentAddress,
            ToResidenceInputs(application.Residences));
        if (blockers.Count > 0)
            return Result.Failure("Fix the validation issues on the summary before submitting.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var hasLease = UnitAvailability.HasActiveLease(application.Unit.Leases, today);
        var check = ApplicationRules.EnsureCanSubmit(application.Status, hasLease);
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

    public async Task<Result<ResidenceSaveResult>> AddResidenceAsync(AddResidenceCommand command, CancellationToken ct = default)
    {
        var application = await _applications.GetWithResidencesAsync(command.ApplicationId, ct);
        if (application is null)
            return Result.Failure<ResidenceSaveResult>("Application not found.");

        var access = EnsureApplicantAccess(application, command.UserId, command.IsManager);
        if (access.IsFailure)
            return Result.Failure<ResidenceSaveResult>(access.Error!);

        var edit = ApplicationRules.EnsureCanEdit(application.Status);
        if (edit.IsFailure)
            return Result.Failure<ResidenceSaveResult>(edit.Error!);

        var address = ApplicationSectionRules.NormalizeOptional(command.Address);
        var landlordName = ApplicationSectionRules.NormalizeOptional(command.LandlordName);
        var landlordPhone = ApplicationSectionRules.NormalizeOptional(command.LandlordPhone);

        var storage = EnsureResidenceStorage(address, landlordName, landlordPhone);
        if (storage.IsFailure)
            return Result.Failure<ResidenceSaveResult>(storage.Error!);

        var residence = new ResidenceHistory
        {
            RentalApplicationId = command.ApplicationId,
            Address = address,
            LandlordName = landlordName,
            LandlordPhone = landlordPhone,
            MoveInDate = command.MoveInDate,
            MoveOutDate = command.MoveOutDate
        };
        await _applications.AddResidenceAsync(residence, ct);

        var errors = ApplicationSectionRules.ValidateResidenceHistory(ToResidenceInputs(application.Residences));
        var isValid = errors.Count == 0;
        application.ResidenceHistorySaved = isValid;
        application.UpdatedAtUtc = DateTime.UtcNow;
        await _applications.SaveChangesAsync(ct);

        return Result.Success(new ResidenceSaveResult(residence.Id, isValid, errors));
    }

    public async Task<Result<SaveSectionResult>> UpdateResidenceAsync(UpdateResidenceCommand command, CancellationToken ct = default)
    {
        var application = await _applications.GetWithResidencesAsync(command.ApplicationId, ct);
        if (application is null)
            return Result.Failure<SaveSectionResult>("Application not found.");

        var access = EnsureApplicantAccess(application, command.UserId, command.IsManager);
        if (access.IsFailure)
            return Result.Failure<SaveSectionResult>(access.Error!);

        var edit = ApplicationRules.EnsureCanEdit(application.Status);
        if (edit.IsFailure)
            return Result.Failure<SaveSectionResult>(edit.Error!);

        var residence = application.Residences.FirstOrDefault(r => r.Id == command.ResidenceId)
            ?? await _applications.GetResidenceAsync(command.ApplicationId, command.ResidenceId, ct);
        if (residence is null)
            return Result.Failure<SaveSectionResult>("Residence not found.");

        var address = ApplicationSectionRules.NormalizeOptional(command.Address);
        var landlordName = ApplicationSectionRules.NormalizeOptional(command.LandlordName);
        var landlordPhone = ApplicationSectionRules.NormalizeOptional(command.LandlordPhone);

        var storage = EnsureResidenceStorage(address, landlordName, landlordPhone);
        if (storage.IsFailure)
            return Result.Failure<SaveSectionResult>(storage.Error!);

        residence.Address = address;
        residence.LandlordName = landlordName;
        residence.LandlordPhone = landlordPhone;
        residence.MoveInDate = command.MoveInDate;
        residence.MoveOutDate = command.MoveOutDate;

        if (!application.Residences.Any(r => r.Id == residence.Id))
            application.Residences.Add(residence);

        var errors = ApplicationSectionRules.ValidateResidenceHistory(ToResidenceInputs(application.Residences));
        var isValid = errors.Count == 0;
        application.ResidenceHistorySaved = isValid;
        application.UpdatedAtUtc = DateTime.UtcNow;
        await _applications.SaveChangesAsync(ct);

        return Result.Success(isValid ? SaveSectionResult.Valid() : SaveSectionResult.Invalid(errors));
    }

    public async Task<Result> DeleteResidenceAsync(DeleteResidenceCommand command, CancellationToken ct = default)
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

        var residence = application.Residences.FirstOrDefault(r => r.Id == command.ResidenceId)
            ?? await _applications.GetResidenceAsync(command.ApplicationId, command.ResidenceId, ct);
        if (residence is null)
            return Result.Failure("Residence not found.");

        _applications.RemoveResidence(residence);
        application.Residences.Remove(residence);

        var errors = ApplicationSectionRules.ValidateResidenceHistory(ToResidenceInputs(application.Residences));
        application.ResidenceHistorySaved = errors.Count == 0;
        application.UpdatedAtUtc = DateTime.UtcNow;
        await _applications.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ClaimAsync(ClaimApplicationCommand command, CancellationToken ct = default)
    {
        await using var transaction = await _applications.BeginSerializableAsync(ct);
        try
        {
            var application = await _applications.GetByIdAsync(command.ApplicationId, ct);
            if (application is null)
                return Result.Failure("Application not found.");

            var claimCheck = ApplicationRules.EnsureCanClaim(application.Status);
            if (claimCheck.IsFailure)
                return claimCheck;

            var from = application.Status;
            application.Status = ApplicationStatus.UnderReview;
            application.ClaimedByUserId = command.ManagerUserId;
            application.ClaimedAtUtc = DateTime.UtcNow;
            application.UpdatedAtUtc = DateTime.UtcNow;
            AddHistory(
                application,
                from,
                ApplicationStatus.UnderReview,
                command.ManagerUserId,
                command.ManagerDisplayName,
                "Claimed for review");
            await _applications.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) when (_applications.IsSerializationFailure(ex))
        {
            return Result.Failure("This application was claimed by another manager. Reload and try again.");
        }
    }

    public async Task<Result> ReleaseAsync(ReleaseApplicationCommand command, CancellationToken ct = default)
    {
        var application = await _applications.GetByIdAsync(command.ApplicationId, ct);
        if (application is null)
            return Result.Failure("Application not found.");

        var releaseCheck = ApplicationRules.EnsureCanRelease(
            application.Status, application.ClaimedByUserId, command.ManagerUserId);
        if (releaseCheck.IsFailure)
            return releaseCheck;

        var from = application.Status;
        application.Status = ApplicationStatus.Submitted;
        ClearClaim(application);
        application.UpdatedAtUtc = DateTime.UtcNow;
        AddHistory(
            application,
            from,
            ApplicationStatus.Submitted,
            command.ManagerUserId,
            command.ManagerDisplayName,
            "Released back to review queue");
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

        var reviewCheck = ApplicationRules.EnsureCanCompleteReview(
            application.Status, application.ClaimedByUserId, command.ManagerUserId);
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

            var reviewCheck = ApplicationRules.EnsureCanCompleteReview(
                application.Status, application.ClaimedByUserId, command.ManagerUserId);
            if (reviewCheck.IsFailure)
                return reviewCheck;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var leaseCheck = ApplicationRules.EnsureUnitCanBeApproved(
                UnitAvailability.HasActiveLease(application.Unit.Leases, today));
            if (leaseCheck.IsFailure)
                return leaseCheck;

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
        ClearClaim(application);
        application.UpdatedAtUtc = DateTime.UtcNow;

        if (command.Outcome == ReviewOutcome.Return)
            application.CurrentSection = ApplicationWizardSection.ApplicantInfo;

        AddHistory(application, from, to, command.ManagerUserId, command.ManagerDisplayName, command.Comment);
    }

    private static void ClearClaim(RentalApplication application)
    {
        application.ClaimedByUserId = null;
        application.ClaimedAtUtc = null;
    }

    private static Result EnsureApplicantAccess(RentalApplication application, string userId, bool isManager)
    {
        if (isManager)
            return Result.Success();

        return application.ApplicantUserId == userId
            ? Result.Success()
            : Result.Failure("You do not own this application.");
    }

    private static IReadOnlyList<ResidenceInput> ToResidenceInputs(IEnumerable<ResidenceHistory> residences) =>
        residences
            .OrderBy(r => r.MoveInDate ?? DateOnly.MaxValue)
            .ThenBy(r => r.Id)
            .Select(r => new ResidenceInput(r.Address, r.LandlordName, r.LandlordPhone, r.MoveInDate, r.MoveOutDate))
            .ToList();

    private static Result EnsureApplicantStorage(string? fullName, string? phone, string? email, string? currentAddress)
    {
        if (fullName?.Length > ApplicationSectionRules.FullNameStorageLength)
            return Result.Failure("Full name exceeds the maximum allowed length.");
        if (phone?.Length > ApplicationSectionRules.PhoneStorageLength)
            return Result.Failure("Phone exceeds the maximum allowed length.");
        if (email?.Length > ApplicationSectionRules.EmailStorageLength)
            return Result.Failure("Email exceeds the maximum allowed length.");
        if (currentAddress?.Length > ApplicationSectionRules.CurrentAddressStorageLength)
            return Result.Failure("Current address exceeds the maximum allowed length.");
        return Result.Success();
    }

    private static Result EnsureResidenceStorage(string? address, string? landlordName, string? landlordPhone)
    {
        if (address?.Length > ApplicationSectionRules.ResidenceAddressStorageLength)
            return Result.Failure("Address exceeds the maximum allowed length.");
        if (landlordName?.Length > ApplicationSectionRules.LandlordNameStorageLength)
            return Result.Failure("Landlord name exceeds the maximum allowed length.");
        if (landlordPhone?.Length > ApplicationSectionRules.LandlordPhoneStorageLength)
            return Result.Failure("Landlord phone exceeds the maximum allowed length.");
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
