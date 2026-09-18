using Microsoft.AspNetCore.Mvc;
using RentalApp.Web.ViewModels.Applications;

namespace RentalApp.Web.ViewComponents;

public class ApplicationGridViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(ApplicationGridViewModel model) => View(model);
}
