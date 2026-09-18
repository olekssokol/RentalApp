using RentalApp.Domain.Entities;

namespace RentalApp.Domain.Services;

public static class ApplicationMembership
{
    public static bool IsMember(RentalApplication application, string userId) =>
        application.Applicants.Any(a => a.UserId == userId);

    public const string StaleSectionMessage =
        "This section was changed by another applicant. Reload the latest version and try again.";
}
