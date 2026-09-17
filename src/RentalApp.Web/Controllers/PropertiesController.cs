using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentalApp.Application.Properties;
using RentalApp.Application.Units;
using RentalApp.Infrastructure.Identity;
using RentalApp.Web.ViewModels.Properties;

namespace RentalApp.Web.Controllers;

[Authorize(Roles = AppRoles.PropertyManager)]
public class PropertiesController : Controller
{
    private readonly IPropertyService _properties;
    private readonly IUnitService _units;

    public PropertiesController(IPropertyService properties, IUnitService units)
    {
        _properties = properties;
        _units = units;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var items = await _properties.GetAllAsync(ct);
        return View(items);
    }

    [HttpGet]
    public IActionResult CreateModal() => PartialView("Partials/_PropertyForm", new PropertyFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateModal(PropertyFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return PartialView("Partials/_PropertyForm", model);

        var result = await _properties.CreateAsync(
            new CreatePropertyCommand(model.Name, model.AddressLine1, model.City, model.State, model.PostalCode), ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return PartialView("Partials/_PropertyForm", model);
        }

        return Ok(new { success = true, refreshUrl = Url.Action(nameof(Index)) });
    }

    [HttpGet]
    public async Task<IActionResult> EditModal(int id, CancellationToken ct)
    {
        var property = await _properties.GetByIdAsync(id, ct);
        if (property is null)
            return NotFound();

        return PartialView("Partials/_PropertyForm", new PropertyFormViewModel
        {
            Id = property.Id,
            Name = property.Name,
            AddressLine1 = property.AddressLine1,
            City = property.City,
            State = property.State,
            PostalCode = property.PostalCode
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditModal(PropertyFormViewModel model, CancellationToken ct)
    {
        if (!model.Id.HasValue)
            return BadRequest();

        if (!ModelState.IsValid)
            return PartialView("Partials/_PropertyForm", model);

        var result = await _properties.UpdateAsync(
            new UpdatePropertyCommand(model.Id.Value, model.Name, model.AddressLine1, model.City, model.State, model.PostalCode), ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return PartialView("Partials/_PropertyForm", model);
        }

        return Ok(new { success = true, refreshUrl = Url.Action(nameof(Index)) });
    }

    [HttpGet]
    public async Task<IActionResult> DeleteModal(int id, CancellationToken ct)
    {
        var property = await _properties.GetByIdAsync(id, ct);
        if (property is null)
            return NotFound();

        return PartialView("Partials/_DeletePropertyModal", new DeletePropertyFormViewModel
        {
            Id = property.Id,
            Name = property.Name
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(DeletePropertyFormViewModel model, CancellationToken ct)
    {
        var property = await _properties.GetByIdAsync(model.Id, ct);
        if (property is null)
            return NotFound();

        model.Name = property.Name;
        var result = await _properties.DeleteAsync(model.Id, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return PartialView("Partials/_DeletePropertyModal", model);
        }

        return Ok(new { success = true, refreshUrl = Url.Action(nameof(Index)) });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var property = await _properties.GetByIdAsync(id, ct);
        if (property is null)
            return NotFound();

        return View(new PropertyDetailsViewModel
        {
            Property = property,
            Units = await _units.GetByPropertyAsync(id, ct)
        });
    }
}
