using RentalApp.Application.Applications;
using RentalApp.Domain.Enums;

namespace RentalApp.Web.ViewModels.Applications;

public class ApplicationGridViewModel
{
    public ApplicationStatus? Status { get; init; }
    public int? PropertyId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public ApplicationSortField Sort { get; init; } = ApplicationSortField.Updated;
    public ApplicationSortDirection Direction { get; init; } = ApplicationSortDirection.Descending;
    public bool IsManager { get; init; }
}
