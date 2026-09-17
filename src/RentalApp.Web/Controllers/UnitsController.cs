using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using RentalApp.Application.Units;
using RentalApp.Infrastructure.Identity;
using RentalApp.Web.ViewModels.Units;

namespace RentalApp.Web.Controllers;

[Authorize]
public class UnitsController : Controller
{
    private readonly IUnitService _units;

    public UnitsController(IUnitService units) => _units = units;

    [Authorize(Roles = AppRoles.Applicant)]
    public async Task<IActionResult> Available(CancellationToken ct)
    {
        var units = await _units.GetAvailableAsync(ct);
        return View(units);
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    public async Task<IActionResult> ByProperty(int propertyId, CancellationToken ct)
    {
        var units = await _units.GetByPropertyAsync(propertyId, ct);
        return PartialView("Partials/_UnitList", units);
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpGet]
    public async Task<IActionResult> CreateModal(int propertyId, CancellationToken ct)
    {
        var model = new UnitFormViewModel
        {
            PropertyId = propertyId,
            UnitTypes = await BuildUnitTypeSelectAsync(null, ct)
        };
        return PartialView("Partials/_UnitForm", model);
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateModal(UnitFormViewModel model, CancellationToken ct)
    {
        model.UnitTypes = await BuildUnitTypeSelectAsync(null, ct);
        if (!ModelState.IsValid)
            return PartialView("Partials/_UnitForm", model);

        var result = await _units.CreateAsync(
            new CreateUnitCommand(model.PropertyId, model.UnitNumber, model.Bedrooms, model.MonthlyRent, model.UnitTypeId), ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return PartialView("Partials/_UnitForm", model);
        }

        return Ok(new { success = true, refreshTarget = "#unit-list", refreshUrl = Url.Action(nameof(ByProperty), new { propertyId = model.PropertyId }) });
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpGet]
    public async Task<IActionResult> EditModal(int id, CancellationToken ct)
    {
        var unit = await _units.GetByIdAsync(id, ct);
        if (unit is null)
            return NotFound();

        var model = new UnitFormViewModel
        {
            Id = unit.Id,
            PropertyId = unit.PropertyId,
            UnitNumber = unit.UnitNumber,
            Bedrooms = unit.Bedrooms,
            MonthlyRent = unit.MonthlyRent,
            UnitTypeId = unit.UnitTypeId,
            UnitTypes = await BuildUnitTypeSelectAsync(unit.UnitTypeId, ct)
        };
        return PartialView("Partials/_UnitForm", model);
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditModal(UnitFormViewModel model, CancellationToken ct)
    {
        if (!model.Id.HasValue)
            return BadRequest();

        var existing = await _units.GetByIdAsync(model.Id.Value, ct);
        model.UnitTypes = await BuildUnitTypeSelectAsync(existing?.UnitTypeId, ct);
        if (!ModelState.IsValid)
            return PartialView("Partials/_UnitForm", model);

        var result = await _units.UpdateAsync(
            new UpdateUnitCommand(model.Id.Value, model.UnitNumber, model.Bedrooms, model.MonthlyRent, model.UnitTypeId), ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return PartialView("Partials/_UnitForm", model);
        }

        return Ok(new { success = true, refreshTarget = "#unit-list", refreshUrl = Url.Action(nameof(ByProperty), new { propertyId = model.PropertyId }) });
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpGet]
    public async Task<IActionResult> DeleteModal(int id, CancellationToken ct)
    {
        var unit = await _units.GetByIdAsync(id, ct);
        if (unit is null)
            return NotFound();

        return PartialView("Partials/_DeleteUnitModal", new DeleteUnitFormViewModel
        {
            Id = unit.Id,
            PropertyId = unit.PropertyId,
            UnitNumber = unit.UnitNumber
        });
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(DeleteUnitFormViewModel model, CancellationToken ct)
    {
        var unit = await _units.GetByIdAsync(model.Id, ct);
        if (unit is null)
            return NotFound();

        model.PropertyId = unit.PropertyId;
        model.UnitNumber = unit.UnitNumber;
        var result = await _units.DeleteAsync(model.Id, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return PartialView("Partials/_DeleteUnitModal", model);
        }

        return Ok(new { success = true, refreshTarget = "#unit-list", refreshUrl = Url.Action(nameof(ByProperty), new { propertyId = model.PropertyId }) });
    }

    private async Task<IEnumerable<SelectListItem>> BuildUnitTypeSelectAsync(int? currentUnitTypeId, CancellationToken ct)
    {
        var types = await _units.GetUnitTypesForSelectAsync(currentUnitTypeId, ct);
        return types.Select(t => new SelectListItem
        {
            Value = t.Id.ToString(),
            Text = t.IsActive ? t.Name : $"{t.Name} (inactive)",
            Selected = currentUnitTypeId == t.Id
        });
    }
}
