namespace RentalApp.Infrastructure.Identity;

public static class AppRoles
{
    public const string Applicant = "Applicant";
    public const string PropertyManager = "PropertyManager";

    public static readonly string[] All = [Applicant, PropertyManager];
}
