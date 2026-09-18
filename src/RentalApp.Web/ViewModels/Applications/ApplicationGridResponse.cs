namespace RentalApp.Web.ViewModels.Applications;

public record ApplicationGridResponse(
    IReadOnlyList<ApplicationGridRowResponse> Rows,
    int FilteredTotal,
    int Page,
    int PageSize);

public record ApplicationGridRowResponse(
    int Id,
    string ApplicantName,
    string PropertyName,
    string UnitNumber,
    string Status,
    DateTime UpdatedAtUtc,
    string OpenUrl,
    string? ClaimUrl,
    string? ReviewUrl,
    string? ReleaseUrl,
    string? ClaimedByDisplayName);
