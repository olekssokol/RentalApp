using Microsoft.AspNetCore.Mvc;
using RentalApp.Domain.Enums;

namespace RentalApp.Web.ViewComponents;

public class ApplicationHeaderViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string propertyName, string unitNumber, ApplicationStatus status)
        => View(new ApplicationHeaderModel(propertyName, unitNumber, status));
}
