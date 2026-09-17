using Microsoft.AspNetCore.Mvc.Rendering;
using RentalApp.Application.Applications;
using RentalApp.Domain.Enums;

namespace RentalApp.Web.ViewModels.Applications;

public class ApplicationListViewModel
{
    public ApplicationStatus? Status { get; set; }
    public int? PropertyId { get; set; }
    public IReadOnlyList<ApplicationListItemDto> Items { get; set; } = [];
    public IEnumerable<SelectListItem> Properties { get; set; } = [];
    public bool IsManager { get; set; }
}
