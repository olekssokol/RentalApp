using System.ComponentModel.DataAnnotations;
using RentalApp.Domain.Enums;

namespace RentalApp.Domain.Services;

/// <summary>
/// Single source of business validation for application wizard sections.
/// Errors are recalculated from persisted data; they are never stored.
/// </summary>
public static class ApplicationSectionRules
{
    public const int FullNameMaxLength = 200;
    public const int PhoneMaxLength = 50;
    public const int EmailMaxLength = 256;
    public const int CurrentAddressMaxLength = 500;
    public const int ResidenceAddressMaxLength = 500;
    public const int LandlordNameMaxLength = 200;
    public const int LandlordPhoneMaxLength = 50;

    // Storage ceilings are higher so values past business max can still persist as invalid drafts.
    public const int FullNameStorageLength = 400;
    public const int PhoneStorageLength = 100;
    public const int EmailStorageLength = 512;
    public const int CurrentAddressStorageLength = 1000;
    public const int ResidenceAddressStorageLength = 1000;
    public const int LandlordNameStorageLength = 400;
    public const int LandlordPhoneStorageLength = 100;

    public static IReadOnlyDictionary<string, string[]> ValidateApplicantInfo(
        string? fullName, string? phone, string? email, string? currentAddress)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        Require(errors, nameof(FullName), fullName, "Full name is required.", FullNameMaxLength, "Full name cannot exceed 200 characters.");
        Require(errors, nameof(Phone), phone, "Phone is required.", PhoneMaxLength, "Phone cannot exceed 50 characters.");
        Require(errors, nameof(Email), email, "Email is required.", EmailMaxLength, "Email cannot exceed 256 characters.");
        if (!string.IsNullOrWhiteSpace(email) && !new EmailAddressAttribute().IsValid(email.Trim()))
            Add(errors, nameof(Email), "Enter a valid email address.");
        Require(errors, nameof(CurrentAddress), currentAddress, "Current address is required.", CurrentAddressMaxLength, "Current address cannot exceed 500 characters.");

        return ToResult(errors);
    }

    public static IReadOnlyDictionary<string, string[]> ValidateResidenceHistory(
        IReadOnlyList<ResidenceInput> residences)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (residences.Count == 0)
        {
            Add(errors, "Residences", "Add at least one prior residence before continuing.");
            return ToResult(errors);
        }

        for (var i = 0; i < residences.Count; i++)
        {
            var r = residences[i];
            var prefix = $"Residences[{i}]";
            Require(errors, $"{prefix}.Address", r.Address, "Address is required.", ResidenceAddressMaxLength, "Address cannot exceed 500 characters.");
            Require(errors, $"{prefix}.LandlordName", r.LandlordName, "Landlord name is required.", LandlordNameMaxLength, "Landlord name cannot exceed 200 characters.");
            Require(errors, $"{prefix}.LandlordPhone", r.LandlordPhone, "Landlord phone is required.", LandlordPhoneMaxLength, "Landlord phone cannot exceed 50 characters.");

            if (r.MoveInDate is null || r.MoveInDate == DateOnly.MinValue)
                Add(errors, $"{prefix}.MoveInDate", "Move-in date is required.");
            else if (r.MoveOutDate.HasValue && r.MoveOutDate.Value < r.MoveInDate.Value)
                Add(errors, $"{prefix}.MoveOutDate", "Move-out date cannot be before move-in date.");
        }

        return ToResult(errors);
    }

    public static IReadOnlyList<SubmissionBlocker> GetSubmissionBlockers(
        string? fullName,
        string? phone,
        string? email,
        string? currentAddress,
        IReadOnlyList<ResidenceInput> residences)
    {
        var blockers = new List<SubmissionBlocker>();

        foreach (var (field, messages) in ValidateApplicantInfo(fullName, phone, email, currentAddress))
        {
            foreach (var message in messages)
                blockers.Add(new SubmissionBlocker(ApplicationWizardSection.ApplicantInfo, field, message));
        }

        foreach (var (field, messages) in ValidateResidenceHistory(residences))
        {
            foreach (var message in messages)
                blockers.Add(new SubmissionBlocker(ApplicationWizardSection.ResidenceHistory, field, message));
        }

        return blockers;
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Require(
        Dictionary<string, List<string>> errors,
        string field,
        string? value,
        string requiredMessage,
        int maxLength,
        string maxMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(errors, field, requiredMessage);
            return;
        }

        if (value.Trim().Length > maxLength)
            Add(errors, field, maxMessage);
    }

    private static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var list))
        {
            list = [];
            errors[field] = list;
        }

        list.Add(message);
    }

    private static IReadOnlyDictionary<string, string[]> ToResult(Dictionary<string, List<string>> errors) =>
        errors.ToDictionary(k => k.Key, v => v.Value.ToArray(), StringComparer.Ordinal);

    // Field name constants for applicant keys used by Require via nameof patterns above.
    private const string FullName = "FullName";
    private const string Phone = "Phone";
    private const string Email = "Email";
    private const string CurrentAddress = "CurrentAddress";
}
