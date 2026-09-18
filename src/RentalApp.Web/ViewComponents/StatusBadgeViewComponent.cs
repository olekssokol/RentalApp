using Microsoft.AspNetCore.Mvc;
using RentalApp.Domain.Enums;

namespace RentalApp.Web.ViewComponents;

public class StatusBadgeViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(ApplicationStatus status)
    {
        var (css, label) = status switch
        {
            ApplicationStatus.Draft => ("draft", "Draft"),
            ApplicationStatus.Submitted => ("submitted", "Submitted"),
            ApplicationStatus.Returned => ("returned", "Returned"),
            ApplicationStatus.Approved => ("approved", "Approved"),
            ApplicationStatus.Denied => ("denied", "Denied"),
            ApplicationStatus.Withdrawn => ("withdrawn", "Withdrawn"),
            ApplicationStatus.UnderReview => ("underreview", "Under Review"),
            _ => ("draft", status.ToString())
        };
        return View(new StatusBadgeModel(css, label));
    }
}
