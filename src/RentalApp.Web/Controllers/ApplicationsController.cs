using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using RentalApp.Application.Applications;
using RentalApp.Application.Properties;
using RentalApp.Domain.Common;
using RentalApp.Domain.Enums;
using RentalApp.Infrastructure.Identity;
using RentalApp.Web.Extensions;
using RentalApp.Web.ViewModels.Applications;

namespace RentalApp.Web.Controllers;

[Authorize]
public class ApplicationsController : Controller
{
    private readonly IApplicationQueryService _queries;
    private readonly IApplicationCommandService _commands;
    private readonly IPropertyManagerNoteService _notes;
    private readonly IPropertyService _properties;

    public ApplicationsController(
        IApplicationQueryService queries,
        IApplicationCommandService commands,
        IPropertyManagerNoteService notes,
        IPropertyService properties)
    {
        _queries = queries;
        _commands = commands;
        _notes = notes;
        _properties = properties;
    }

    public async Task<IActionResult> Index(
        ApplicationStatus? status,
        int? propertyId,
        int page = 1,
        int pageSize = 10,
        ApplicationSortField sort = ApplicationSortField.Updated,
        ApplicationSortDirection direction = ApplicationSortDirection.Descending,
        CancellationToken ct = default)
    {
        var isManager = User.IsManager();
        var properties = await _properties.GetAllAsync(ct);
        var query = new ApplicationListQuery(User.GetUserId(), isManager, status, propertyId, page, pageSize, sort, direction);

        return View(new ApplicationListViewModel
        {
            Status = status,
            PropertyId = propertyId,
            IsManager = isManager,
            Page = query.NormalizedPage,
            PageSize = query.NormalizedPageSize,
            Sort = query.NormalizedSort,
            Direction = query.NormalizedDirection,
            Properties = properties.Select(p => new SelectListItem(p.Name, p.Id.ToString(), propertyId == p.Id))
        });
    }

    /// <summary>Returns one database-filtered, sorted and paged application-grid page.</summary>
    [HttpGet("/api/applications/grid")]
    [Produces("application/json")]
    [ProducesResponseType<ApplicationGridResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApplicationGridResponse>> Grid(
        ApplicationStatus? status,
        int? propertyId,
        int page = 1,
        int pageSize = 10,
        ApplicationSortField sort = ApplicationSortField.Updated,
        ApplicationSortDirection direction = ApplicationSortDirection.Descending,
        CancellationToken ct = default)
    {
        var isManager = User.IsManager();
        var result = await _queries.ListAsync(new ApplicationListQuery(
            User.GetUserId(), isManager, status, propertyId, page, pageSize, sort, direction), ct);
        var rows = result.Items.Select(item => new ApplicationGridRowResponse(
            item.Id,
            item.ApplicantName,
            item.PropertyName,
            item.UnitNumber,
            item.Status.ToString(),
            item.UpdatedAtUtc,
            Url.Action(nameof(Wizard), new { id = item.Id })!,
            isManager && item.Status == ApplicationStatus.Submitted
                ? Url.Action(nameof(ReviewModal), new { id = item.Id })
                : null)).ToList();

        return Ok(new ApplicationGridResponse(rows, result.FilteredTotal, result.Page, result.PageSize));
    }

    [HttpGet]
    public async Task<IActionResult> Wizard(int id, ApplicationWizardSection? section, CancellationToken ct)
    {
        var detail = await GetDetailAsync(id, ct);
        if (detail is null)
            return NotFound();

        var isManager = User.IsManager();
        if (isManager)
        {
            var notes = await _notes.ListAsync(id, isManager, ct);
            if (notes.IsSuccess)
                ViewData["PropertyManagerNotes"] = BuildNotesViewModel(id, notes.Value!);
        }

        return View(ApplicationWizardViewModel.From(detail, isManager, section));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Wizard(ApplicationWizardViewModel model, CancellationToken ct)
    {
        var detail = await GetDetailAsync(model.Id, ct);
        if (detail is null)
            return NotFound();

        var userId = User.GetUserId();
        var isManager = User.IsManager();
        var canEdit = detail.CanEdit && !isManager;
        var action = model.Action ?? "Continue";

        if (action == "Back")
        {
            if (!canEdit)
                return RedirectToAction(nameof(Wizard), new { id = model.Id });

            var back = await _commands.GoBackAsync(new GoBackCommand(model.Id, userId, isManager), ct);
            if (back.IsFailure)
                TempData["Error"] = back.Error;
            return RedirectToAction(nameof(Wizard), new { id = model.Id });
        }

        if (action == "Submit")
        {
            var submit = await _commands.SubmitAsync(new SubmitApplicationCommand(model.Id, userId), ct);
            if (submit.IsFailure)
            {
                TempData["Error"] = submit.Error;
                return RedirectToAction(nameof(Wizard), new { id = model.Id });
            }

            TempData["Success"] = "Application submitted.";
            return RedirectToAction(nameof(Wizard), new { id = model.Id });
        }

        if (!canEdit)
            return RedirectToAction(nameof(Wizard), new { id = model.Id });

        if (detail.CurrentSection == ApplicationWizardSection.ApplicantInfo)
        {
            var vm = ApplicationWizardViewModel.From(detail, isManager);
            vm.FullName = model.FullName;
            vm.Phone = model.Phone;
            vm.Email = model.Email;
            vm.CurrentAddress = model.CurrentAddress;

            if (!ModelState.IsValid)
                return View(vm);

            var save = await _commands.SaveApplicantInfoAsync(
                new SaveApplicantInfoCommand(model.Id, userId, isManager, model.FullName!, model.Phone!, model.Email!, model.CurrentAddress!, Advance: true), ct);
            if (save.IsFailure)
            {
                ModelState.AddModelError(string.Empty, save.Error!);
                return View(vm);
            }
        }
        else if (detail.CurrentSection == ApplicationWizardSection.ResidenceHistory)
        {
            var save = await _commands.SaveResidenceHistoryAsync(
                new SaveResidenceHistoryCommand(model.Id, userId, isManager, Advance: true), ct);
            if (save.IsFailure)
            {
                var vm = ApplicationWizardViewModel.From(detail, isManager);
                ModelState.AddModelError(string.Empty, save.Error!);
                return View(vm);
            }
        }

        return RedirectToAction(nameof(Wizard), new { id = model.Id });
    }

    [Authorize(Roles = AppRoles.Applicant)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int id, CancellationToken ct)
    {
        var result = await _commands.WithdrawAsync(new WithdrawApplicationCommand(id, User.GetUserId()), ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Application withdrawn." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResidenceModal(int applicationId, int? id, CancellationToken ct)
    {
        var detail = await GetDetailAsync(applicationId, ct);
        if (detail is null)
            return NotFound();
        if (!detail.CanEdit || User.IsManager())
            return Forbid();

        if (id is null)
            return PartialView("Partials/_ResidenceModal", new ResidenceFormViewModel { ApplicationId = applicationId, MoveInDate = DateOnly.FromDateTime(DateTime.Today.AddYears(-2)) });

        var residence = detail.Residences.FirstOrDefault(r => r.Id == id);
        if (residence is null)
            return NotFound();

        return PartialView("Partials/_ResidenceModal", new ResidenceFormViewModel
        {
            ApplicationId = applicationId,
            Id = residence.Id,
            Address = residence.Address,
            LandlordName = residence.LandlordName,
            LandlordPhone = residence.LandlordPhone,
            MoveInDate = residence.MoveInDate,
            MoveOutDate = residence.MoveOutDate
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResidenceModal(ResidenceFormViewModel model, CancellationToken ct)
    {
        var detail = await GetDetailAsync(model.ApplicationId, ct);
        if (detail is null)
            return NotFound();
        if (!detail.CanEdit || User.IsManager())
            return Forbid();

        if (model.MoveInDate == DateOnly.MinValue)
            ModelState.AddModelError(nameof(model.MoveInDate), "Move-in date is required.");

        if (!ModelState.IsValid)
            return PartialView("Partials/_ResidenceModal", model);

        var userId = User.GetUserId();
        var isManager = User.IsManager();
        Result result;

        if (model.Id is null)
        {
            var create = await _commands.AddResidenceAsync(
                new AddResidenceCommand(model.ApplicationId, userId, isManager, model.Address, model.LandlordName, model.LandlordPhone, model.MoveInDate!.Value, model.MoveOutDate), ct);
            result = create.IsSuccess ? Result.Success() : Result.Failure(create.Error!);
        }
        else
        {
            result = await _commands.UpdateResidenceAsync(
                new UpdateResidenceCommand(model.ApplicationId, model.Id.Value, userId, isManager, model.Address, model.LandlordName, model.LandlordPhone, model.MoveInDate!.Value, model.MoveOutDate), ct);
        }

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return PartialView("Partials/_ResidenceModal", model);
        }

        return Ok(new { success = true, refreshUrl = Url.Action(nameof(Wizard), new { id = model.ApplicationId }) });
    }

    [HttpGet]
    public async Task<IActionResult> DeleteResidenceModal(int applicationId, int id, CancellationToken ct)
    {
        var detail = await GetDetailAsync(applicationId, ct);
        if (detail is null)
            return NotFound();
        if (!detail.CanEdit || User.IsManager())
            return Forbid();

        var residence = detail.Residences.FirstOrDefault(r => r.Id == id);
        if (residence is null)
            return NotFound();

        return PartialView("Partials/_DeleteResidenceModal", new DeleteResidenceFormViewModel
        {
            ApplicationId = applicationId,
            Id = residence.Id,
            Address = residence.Address
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteResidence(DeleteResidenceFormViewModel model, CancellationToken ct)
    {
        var detail = await GetDetailAsync(model.ApplicationId, ct);
        if (detail is null)
            return NotFound();
        if (!detail.CanEdit || User.IsManager())
            return Forbid();

        var residence = detail.Residences.FirstOrDefault(r => r.Id == model.Id);
        if (residence is null)
            return NotFound();

        model.Address = residence.Address;
        var result = await _commands.DeleteResidenceAsync(
            new DeleteResidenceCommand(model.ApplicationId, model.Id, User.GetUserId(), User.IsManager()), ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return PartialView("Partials/_DeleteResidenceModal", model);
        }

        return Ok(new { success = true, refreshUrl = Url.Action(nameof(Wizard), new { id = model.ApplicationId }) });
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpGet]
    public async Task<IActionResult> ReviewModal(int id, CancellationToken ct)
    {
        var detail = await GetDetailAsync(id, ct);
        if (detail is null)
            return NotFound();
        if (detail.Status != ApplicationStatus.Submitted)
            return BadRequest("Only submitted applications can be reviewed.");

        return PartialView("Partials/_ReviewModal", new ReviewFormViewModel
        {
            ApplicationId = detail.Id,
            ApplicantName = detail.FullName ?? "",
            PropertyName = detail.PropertyName,
            UnitNumber = detail.UnitNumber
        });
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewModal(ReviewFormViewModel model, CancellationToken ct)
    {
        if (model.Outcome is ReviewOutcome.Return or ReviewOutcome.Deny && string.IsNullOrWhiteSpace(model.Comment))
            ModelState.AddModelError(nameof(model.Comment), "Comment is required for Return and Deny.");

        if (!ModelState.IsValid)
            return PartialView("Partials/_ReviewModal", model);

        var result = await _commands.ReviewAsync(
            new ReviewApplicationCommand(model.ApplicationId, User.GetUserId(), User.GetDisplayName(), model.Outcome, model.Comment), ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return PartialView("Partials/_ReviewModal", model);
        }

        return Ok(new { success = true, refreshUrl = Url.Action(nameof(Wizard), new { id = model.ApplicationId }) });
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpGet]
    public async Task<IActionResult> ManagerNotes(int applicationId, CancellationToken ct)
    {
        var result = await _notes.ListAsync(applicationId, User.IsManager(), ct);
        return result.IsFailure
            ? NotFound()
            : PartialView("Partials/_PropertyManagerNotes", BuildNotesViewModel(applicationId, result.Value!));
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpGet]
    public async Task<IActionResult> ManagerNoteModal(int applicationId, int? id, CancellationToken ct)
    {
        if (id is null)
        {
            var list = await _notes.ListAsync(applicationId, User.IsManager(), ct);
            return list.IsFailure
                ? NotFound()
                : PartialView("Partials/_PropertyManagerNoteModal", new PropertyManagerNoteFormViewModel { ApplicationId = applicationId });
        }

        var result = await _notes.GetAsync(applicationId, id.Value, User.IsManager(), ct);
        return result.IsFailure
            ? NotFound()
            : PartialView("Partials/_PropertyManagerNoteModal", new PropertyManagerNoteFormViewModel
            {
                ApplicationId = applicationId,
                Id = result.Value!.Id,
                Text = result.Value.Text
            });
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ManagerNoteModal(PropertyManagerNoteFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return PartialView("Partials/_PropertyManagerNoteModal", model);

        Result result;
        if (model.Id is null)
        {
            var add = await _notes.AddAsync(new AddPropertyManagerNoteCommand(
                model.ApplicationId, User.GetUserId(), User.GetDisplayName(), model.Text, User.IsManager()), ct);
            result = add.IsSuccess ? Result.Success() : Result.Failure(add.Error!);
        }
        else
        {
            result = await _notes.UpdateAsync(new UpdatePropertyManagerNoteCommand(
                model.ApplicationId, model.Id.Value, model.Text, User.IsManager()), ct);
        }

        if (result.IsFailure)
        {
            ModelState.AddModelError(nameof(model.Text), result.Error!);
            return PartialView("Partials/_PropertyManagerNoteModal", model);
        }

        return Ok(new
        {
            success = true,
            refreshTarget = "#property-manager-notes",
            refreshUrl = Url.Action(nameof(ManagerNotes), new { applicationId = model.ApplicationId })
        });
    }

    private Task<ApplicationDetailDto?> GetDetailAsync(int id, CancellationToken ct) =>
        _queries.GetAsync(id, User.GetUserId(), User.IsManager(), ct);

    private static PropertyManagerNotesViewModel BuildNotesViewModel(
        int applicationId, IReadOnlyList<PropertyManagerNoteDto> notes) =>
        new() { ApplicationId = applicationId, Items = notes };
}
