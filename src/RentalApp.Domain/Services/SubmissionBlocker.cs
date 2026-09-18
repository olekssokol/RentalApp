using RentalApp.Domain.Enums;

namespace RentalApp.Domain.Services;

public sealed record SubmissionBlocker(
    ApplicationWizardSection Section,
    string Field,
    string Message);
