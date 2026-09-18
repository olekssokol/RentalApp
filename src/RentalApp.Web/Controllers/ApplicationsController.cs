using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using RentalApp.Application.Applications;
using RentalApp.Application.Properties;
using RentalApp.Domain.Common;
using RentalApp.Domain.Enums;
using RentalApp.Domain.Services;
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
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicationsController(
        IApplicationQueryService queries,
        IApplicationCommandService commands,
        IPropertyManagerNoteService notes,
        IPropertyService properties,
        UserManager<ApplicationUser> userManager)
    {
        _queries = queries;
        _commands = commands;
        _notes = notes;
        _properties = properties;
        _userManager = userManager;
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
        var userId = User.GetUserId();
        var result = await _queries.ListAsync(new ApplicationListQuery(
            userId, isManager, status, propertyId, page, pageSize, sort, direction), ct);
        var rows = result.Items.Select(item =>
        {
            var isClaimer = isManager
                && item.ClaimedByUserId is not null
                && string.Equals(item.ClaimedByUserId, userId, StringComparison.Ordinal);
            return new ApplicationGridRowResponse(
                item.Id,
                item.ApplicantName,
                item.PropertyName,
                item.UnitNumber,
                item.Status.ToString(),
                item.UpdatedAtUtc,
                Url.Action(nameof(Wizard), new { id = item.Id })!,
                isManager && item.Status == ApplicationStatus.Submitted
                    ? Url.Action(nameof(Claim), new { id = item.Id })
                    : null,
                isClaimer && item.Status == ApplicationStatus.UnderReview
                    ? Url.Action(nameof(ReviewModal), new { id = item.Id })
                    : null,
                isClaimer && item.Status == ApplicationStatus.UnderReview
                    ? Url.Action(nameof(Release), new { id = item.Id })
                    : null,
                item.Status == ApplicationStatus.UnderReview ? item.ClaimedByDisplayName : null);
        }).ToList();

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

        return View(await BuildWizardAsync(detail, isManager, User.GetUserId(), section, ct));
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
        var displaySection = model.DisplaySection;

        if (action == "Back")
        {
            if (!canEdit)
                return RedirectToAction(nameof(Wizard), new { id = model.Id, section = model.PreviousSection });

            if (detail.Members.Count > 1)
                return RedirectToAction(nameof(Wizard), new { id = model.Id, section = model.PreviousSection });

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
                return RedirectToAction(nameof(Wizard), new { id = model.Id, section = ApplicationWizardSection.Summary });
            }

            TempData["Success"] = "Application submitted.";
            return RedirectToAction(nameof(Wizard), new { id = model.Id, section = ApplicationWizardSection.Summary });
        }

        if (!canEdit)
            return RedirectToAction(nameof(Wizard), new { id = model.Id, section = displaySection });

        if (displaySection == ApplicationWizardSection.ApplicantInfo)
        {
            var save = await _commands.SaveApplicantInfoAsync(
                new SaveApplicantInfoCommand(
                    model.Id, userId, isManager, model.FullName, model.Phone, model.Email, model.CurrentAddress,
                    Advance: true, model.ApplicantInfoVersion), ct);
            if (save.IsFailure)
            {
                var vm = await BuildWizardAsync(detail, isManager, userId, displaySection, ct);
                vm.FullName = model.FullName;
                vm.Phone = model.Phone;
                vm.Email = model.Email;
                vm.CurrentAddress = model.CurrentAddress;
                ModelState.AddModelError(string.Empty, save.Error!);
                return View(vm);
            }

            if (!save.Value!.IsValid)
            {
                var refreshed = await GetDetailAsync(model.Id, ct);
                var vm = await BuildWizardAsync(refreshed!, isManager, userId, displaySection, ct);
                vm.FullName = model.FullName;
                vm.Phone = model.Phone;
                vm.Email = model.Email;
                vm.CurrentAddress = model.CurrentAddress;
                AddFieldErrors(save.Value.FieldErrors);
                return View(vm);
            }

            return RedirectToAction(nameof(Wizard), new { id = model.Id, section = ApplicationWizardSection.ResidenceHistory });
        }

        if (displaySection == ApplicationWizardSection.ResidenceHistory)
        {
            var save = await _commands.SaveResidenceHistoryAsync(
                new SaveResidenceHistoryCommand(model.Id, userId, isManager, Advance: true, model.ResidenceHistoryVersion), ct);
            if (save.IsFailure)
            {
                var vm = await BuildWizardAsync(detail, isManager, userId, displaySection, ct);
                ModelState.AddModelError(string.Empty, save.Error!);
                return View(vm);
            }

            if (!save.Value!.IsValid)
            {
                var refreshed = await GetDetailAsync(model.Id, ct);
                var vm = await BuildWizardAsync(refreshed!, isManager, userId, displaySection, ct);
                AddFieldErrors(save.Value.FieldErrors);
                return View(vm);
            }

            return RedirectToAction(nameof(Wizard), new { id = model.Id, section = ApplicationWizardSection.Summary });
        }

        return RedirectToAction(nameof(Wizard), new { id = model.Id, section = displaySection });
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
            return PartialView("Partials/_ResidenceModal", new ResidenceFormViewModel
            {
                ApplicationId = applicationId,
                MoveInDate = DateOnly.FromDateTime(DateTime.Today.AddYears(-2)),
                ExpectedResidenceHistoryVersion = detail.ResidenceHistoryVersion
            });

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
            MoveOutDate = residence.MoveOutDate,
            ExpectedResidenceHistoryVersion = detail.ResidenceHistoryVersion
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

        var userId = User.GetUserId();
        var isManager = User.IsManager();

        if (model.Id is null)
        {
            var create = await _commands.AddResidenceAsync(
                new AddResidenceCommand(
                    model.ApplicationId, userId, isManager, model.Address, model.LandlordName, model.LandlordPhone,
                    model.MoveInDate, model.MoveOutDate, model.ExpectedResidenceHistoryVersion), ct);
            if (create.IsFailure)
            {
                ModelState.AddModelError(string.Empty, create.Error!);
                return PartialView("Partials/_ResidenceModal", model);
            }

            if (!create.Value!.IsValid)
            {
                model.Id = create.Value.ResidenceId;
                var refreshed = await GetDetailAsync(model.ApplicationId, ct);
                model.ExpectedResidenceHistoryVersion = refreshed!.ResidenceHistoryVersion;
                AddResidenceModalFieldErrors(create.Value.FieldErrors, create.Value.ResidenceId, refreshed.Residences);
                return PartialView("Partials/_ResidenceModal", model);
            }
        }
        else
        {
            var update = await _commands.UpdateResidenceAsync(
                new UpdateResidenceCommand(
                    model.ApplicationId, model.Id.Value, userId, isManager, model.Address, model.LandlordName,
                    model.LandlordPhone, model.MoveInDate, model.MoveOutDate, model.ExpectedResidenceHistoryVersion), ct);
            if (update.IsFailure)
            {
                ModelState.AddModelError(string.Empty, update.Error!);
                return PartialView("Partials/_ResidenceModal", model);
            }

            if (!update.Value!.IsValid)
            {
                var refreshed = await GetDetailAsync(model.ApplicationId, ct);
                model.ExpectedResidenceHistoryVersion = refreshed!.ResidenceHistoryVersion;
                AddResidenceModalFieldErrors(update.Value.FieldErrors, model.Id.Value, refreshed.Residences);
                return PartialView("Partials/_ResidenceModal", model);
            }
        }

        return Ok(new { success = true, refreshUrl = Url.Action(nameof(Wizard), new { id = model.ApplicationId, section = ApplicationWizardSection.ResidenceHistory }) });
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
            Address = residence.Address,
            ExpectedResidenceHistoryVersion = detail.ResidenceHistoryVersion
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
            new DeleteResidenceCommand(
                model.ApplicationId, model.Id, User.GetUserId(), User.IsManager(), model.ExpectedResidenceHistoryVersion), ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return PartialView("Partials/_DeleteResidenceModal", model);
        }

        return Ok(new { success = true, refreshUrl = Url.Action(nameof(Wizard), new { id = model.ApplicationId, section = ApplicationWizardSection.ResidenceHistory }) });
    }

    [Authorize(Roles = AppRoles.Applicant)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCoApplicant(int id, string? email, CancellationToken ct)
    {
        var detail = await GetDetailAsync(id, ct);
        if (detail is null)
            return NotFound();
        if (!detail.CanEdit)
            return Forbid();

        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Enter an applicant email.";
            return RedirectToAction(nameof(Wizard), new { id, section = ApplicationWizardSection.Summary });
        }

        var target = await _userManager.FindByEmailAsync(email.Trim());
        if (target is null || !await _userManager.IsInRoleAsync(target, AppRoles.Applicant))
        {
            TempData["Error"] = "No applicant account was found for that email.";
            return RedirectToAction(nameof(Wizard), new { id, section = ApplicationWizardSection.Summary });
        }

        var result = await _commands.AddCoApplicantAsync(
            new AddCoApplicantCommand(id, User.GetUserId(), target.Id), ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Co-applicant added."
            : result.Error;
        return RedirectToAction(nameof(Wizard), new { id, section = ApplicationWizardSection.Summary });
    }

    [Authorize(Roles = AppRoles.Applicant)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCoApplicant(int id, string userId, CancellationToken ct)
    {
        var detail = await GetDetailAsync(id, ct);
        if (detail is null)
            return NotFound();
        if (!detail.CanEdit)
            return Forbid();

        var result = await _commands.RemoveCoApplicantAsync(
            new RemoveCoApplicantCommand(id, User.GetUserId(), userId), ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Co-applicant removed."
            : result.Error;
        return RedirectToAction(nameof(Wizard), new { id, section = ApplicationWizardSection.Summary });
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(int id, CancellationToken ct)
    {
        var result = await _commands.ClaimAsync(
            new ClaimApplicationCommand(id, User.GetUserId(), User.GetDisplayName()), ct);
        if (result.IsFailure)
            TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Wizard), new { id });
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Release(int id, CancellationToken ct)
    {
        var result = await _commands.ReleaseAsync(
            new ReleaseApplicationCommand(id, User.GetUserId(), User.GetDisplayName()), ct);
        if (result.IsFailure)
            TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Wizard), new { id });
    }

    [Authorize(Roles = AppRoles.PropertyManager)]
    [HttpGet]
    public async Task<IActionResult> ReviewModal(int id, CancellationToken ct)
    {
        var detail = await GetDetailAsync(id, ct);
        if (detail is null)
            return NotFound();

        var completeCheck = ApplicationRules.EnsureCanCompleteReview(
            detail.Status, detail.ClaimedByUserId, User.GetUserId());
        if (completeCheck.IsFailure)
            return BadRequest(completeCheck.Error);

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

    private async Task<ApplicationWizardViewModel> BuildWizardAsync(
        ApplicationDetailDto detail,
        bool isManager,
        string currentUserId,
        ApplicationWizardSection? requestedSection,
        CancellationToken ct)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var member in detail.Members)
        {
            var user = await _userManager.FindByIdAsync(member.UserId);
            names[member.UserId] = user?.FullName ?? member.UserId;
        }

        return ApplicationWizardViewModel.From(detail, isManager, currentUserId, requestedSection, names);
    }

    private void AddFieldErrors(IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        foreach (var (key, messages) in fieldErrors)
        {
            foreach (var message in messages)
                ModelState.AddModelError(key, message);
        }
    }

    private void AddResidenceModalFieldErrors(
        IReadOnlyDictionary<string, string[]> fieldErrors,
        int residenceId,
        IReadOnlyList<ResidenceDto> residences)
    {
        var index = -1;
        for (var i = 0; i < residences.Count; i++)
        {
            if (residences[i].Id == residenceId)
            {
                index = i;
                break;
            }
        }

        var prefix = $"Residences[{index}].";
        foreach (var (key, messages) in fieldErrors)
        {
            string formKey;
            if (index >= 0 && key.StartsWith(prefix, StringComparison.Ordinal))
                formKey = key[prefix.Length..];
            else if (key.StartsWith("Residences[", StringComparison.Ordinal))
                continue;
            else
                formKey = key;

            foreach (var message in messages)
                ModelState.AddModelError(formKey, message);
        }
    }

    private static PropertyManagerNotesViewModel BuildNotesViewModel(
        int applicationId, IReadOnlyList<PropertyManagerNoteDto> notes) =>
        new() { ApplicationId = applicationId, Items = notes };
}
