using Microsoft.AspNetCore.Mvc.Rendering;
using RentalApp.Domain.Enums;
using RentalApp.Application.Applications;

namespace RentalApp.Web.ViewModels.Applications;

public class ApplicationListViewModel
{
    public ApplicationStatus? Status { get; set; }
    public int? PropertyId { get; set; }
    public IEnumerable<SelectListItem> Properties { get; set; } = [];
    public bool IsManager { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public ApplicationSortField Sort { get; set; } = ApplicationSortField.Updated;
    public ApplicationSortDirection Direction { get; set; } = ApplicationSortDirection.Descending;
}
